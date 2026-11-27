import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Rate } from 'k6/metrics';

// Tách riêng metric để đối chiếu trực tiếp NFR-PER-01 và NFR-PER-02 trong Report 3
const per01Duration = new Trend('nfr_per_01_duration', true);
const per01ErrorRate = new Rate('nfr_per_01_errors');
const per02Duration = new Trend('nfr_per_02_duration', true);
const per02ErrorRate = new Rate('nfr_per_02_errors');

const BASE_URL = __ENV.BASE_URL || 'https://localhost:7200';
const TEST_EMAIL = __ENV.EMAIL || '';
const TEST_PASSWORD = __ENV.PASSWORD || '';

export const options = {
  insecureSkipTLSVerify: true, // Bỏ qua lỗi SSL self-signed trên localhost
  stages: [
    { duration: '15s', target: 30 }, // Ramp-up lên 30 VUs (có thể tăng thành 50 nếu máy khỏe)
    { duration: '60s', target: 30 }, // Giữ ổn định 30 VUs trong 60 giây
    { duration: '15s', target: 0 },  // Ramp-down
  ],
  thresholds: {
    'nfr_per_01_duration': ['p(95)<2000'],
    'nfr_per_02_duration': ['p(95)<3000'],
    'http_req_failed': ['rate<0.01'],
  },
};

// Đăng nhập 1 lần lấy JWT token hợp lệ cho các API yêu cầu xác thực
export function setup() {
  if (!TEST_EMAIL || !TEST_PASSWORD) {
    console.warn('Chưa truyền EMAIL/PASSWORD -> chỉ chạy các endpoint public.');
    return { token: null };
  }

  const loginRes = http.post(
    `${BASE_URL}/api/auth/login`,
    JSON.stringify({ email: TEST_EMAIL, password: TEST_PASSWORD }),
    { headers: { 'Content-Type': 'application/json' } }
  );

  check(loginRes, { 'login status 200': (r) => r.status === 200 });

  let token = null;
  try {
    const body = loginRes.json();
    token =
      body?.data?.accessToken ||
      body?.data?.token ||
      body?.accessToken ||
      body?.token ||
      null;
  } catch (e) {
    console.error('Không parse được JSON login:', loginRes.body);
  }

  if (!token) {
    console.error('Không lấy được JWT token. Response:', loginRes.status, loginRes.body);
  }
  return { token };
}

export default function (data) {
  const authHeaders = data.token
    ? { headers: { Authorization: `Bearer ${data.token}` } }
    : {};

  // --- Nhóm NFR-PER-01: Standard Interactive APIs (Target p95 <= 2,000 ms) ---
  const resPackages = http.get(`${BASE_URL}/api/trainingpackages`);
  per01Duration.add(resPackages.timings.duration);
  per01ErrorRate.add(resPackages.status >= 400 || resPackages.status === 0);
  check(resPackages, { 'GET /api/trainingpackages 200': (r) => r.status === 200 });

  if (data.token) {
    const resPosts = http.get(`${BASE_URL}/api/posts?page=1&pageSize=10`, authHeaders);
    per01Duration.add(resPosts.timings.duration);
    per01ErrorRate.add(resPosts.status >= 400 || resPosts.status === 0);
    check(resPosts, { 'GET /api/posts 200': (r) => r.status === 200 });

    // --- Nhóm NFR-PER-02: Search, Filter & Analytics APIs (Target p95 <= 3,000 ms) ---
    const resCoaches = http.get(`${BASE_URL}/api/coaches?page=1&pageSize=6`, authHeaders);
    per02Duration.add(resCoaches.timings.duration);
    per02ErrorRate.add(resCoaches.status >= 400 || resCoaches.status === 0);
    check(resCoaches, { 'GET /api/coaches 200': (r) => r.status === 200 });

    const resPurchased = http.get(`${BASE_URL}/api/trainingpackages/purchased?page=1&pageSize=10`, authHeaders);
    per02Duration.add(resPurchased.timings.duration);
    per02ErrorRate.add(resPurchased.status >= 400 || resPurchased.status === 0);
    check(resPurchased, { 'GET /api/trainingpackages/purchased 200': (r) => r.status === 200 });
  }

  sleep(0.5);
}