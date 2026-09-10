# 🏋️ FitSocial - Hướng Dẫn Phát Triển Frontend Cho Thành Viên Trong Nhóm

Tài liệu này hướng dẫn các thành viên trong nhóm cách thiết lập, phát triển và tuân thủ các quy chuẩn lập trình trên dự án Frontend của **FitSocial**.

---

## 1. Tech Stack 

* **Framework:** Next.js (App Router) + React 18/19
* **Ngôn ngữ:** TypeScript (bắt buộc định nghĩa kiểu dữ liệu rõ ràng)
* **Styling:** TailwindCSS + Shadcn UI (Preset Nova: Lucide Icons + Geist Font)
* **Quản lý State:** 
  * Server State & Caching: `@tanstack/react-query`
  * Client / Global State: `zustand`
* **Gọi API & Realtime:** `axios` + `@microsoft/signalr`
* **Form & Validation:** `react-hook-form` + `zod`

---

## 2. Hướng Dẫn Cài Đặt & Chạy Dự Án

### Yêu cầu môi trường:
* **Node.js:** phiên bản `18.18.0` hoặc `20.x` trở lên ([Tải tại đây](https://nodejs.org/))
* **Trình quản lý gói:** `npm` (đi kèm Node.js)

### Các bước khởi chạy:

1. **Mở terminal và di chuyển vào thư mục `frontend`:**
   ```bash
   cd frontend
   ```

2. **Cài đặt các thư viện (dependencies):**
   ```bash
   npm install
   ```

3. **Cấu hình biến môi trường:**
   * Tạo một file có tên `.env.local` tại thư mục gốc của `frontend` (nếu chưa có):
   ```env
   NEXT_PUBLIC_API_URL=https://localhost:7001/api
   NEXT_PUBLIC_SIGNALR_URL=https://localhost:7001/hubs
   ```
   *(Lưu ý: Thay đổi port `7001` thành port thực tế của Backend ASP.NET Core khi chạy).*

4. **Khởi động server dev:**
   ```bash
   npm run dev
   ```
   Truy cập vào trình duyệt: **`http://localhost:3000`**

---

##  3. Cấu Trúc Thư Mục & Phân Chia Trách Nhiệm

Mọi mã nguồn nằm trong thư mục `src/`:

```plaintext
src/
├── app/                               # Routing (Next.js App Router)
│   ├── (auth)/                        # Trang đăng nhập, đăng ký
│   ├── (main)/                        # Trang chính sau khi login (feed, workouts, profile...)
│   ├── layout.tsx                     # Root Layout bọc toàn ứng dụng
│   └── globals.css                    # Cấu hình CSS toàn cục & màu sắc
│
├── components/                        # UI Components
│   ├── ui/                            # Component do Shadcn UI sinh ra (KHÔNG sửa tay tùy tiện)
│   ├── shared/                        # Dùng chung: Navbar, Sidebar, Footer, Loading...
│   └── features/                      # Chia theo module tính năng:
│       ├── auth/                      # LoginForm, RegisterForm...
│       ├── posts/                     # PostCard, CreatePostModal, CommentList...
│       └── workouts/                  # ExerciseCard, RoutineList...
│
├── lib/                               # Cấu hình thư viện ngoài
│   ├── api/axiosClient.ts             # Axios cấu hình sẵn Token & Interceptor
│   ├── signalr/signalrClient.ts       # Kết nối WebSocket Realtime
│   └── utils.ts                       # Hàm tiện ích (hàm cn gom class tailwind)
│
├── services/                          # Hàm gọi API Backend (tương ứng với Controller)
│   ├── auth.service.ts
│   ├── post.service.ts
│   └── user.service.ts
│
├── types/                             # TypeScript Interface / Type (Map với DTOs của Backend)
│   ├── auth.types.ts
│   ├── post.types.ts
│   └── user.types.ts
│
├── hooks/                             # Custom React Hooks tái sử dụng (useAuth, useSignalR...)
└── stores/                            # Zustand store (lưu thông tin user, giỏ hàng, thông báo...)
```

---

##  4. Cách Sử Dụng Thư Viện Giao Diện (Shadcn UI)

Dự án dùng **Shadcn UI**. Khi bạn cần một thành phần UI mới (như Slider, Select, Switch...), **không tự viết từ đầu**, hãy thêm component chính chủ bằng lệnh CLI:

```bash
npx shadcn@latest add <tên-component>
```

*Ví dụ:*
```bash
npx shadcn@latest add select
npx shadcn@latest add switch
npx shadcn@latest add sheet
```

Sau khi chạy, component sẽ tự sinh vào `src/components/ui/` và bạn chỉ cần import vào sử dụng:
```tsx
import { Button } from "@/components/ui/button";
import { Card, CardHeader, CardTitle, CardContent } from "@/components/ui/card";
```

---

##  5. Quy Trình Chuẩn Khi Thêm Một Tính Năng Mới

Ví dụ: Bạn được phân công làm tính năng **"Danh sách bài đăng (Feed / Posts)"**:

### Bước 1: Khai báo Type (DTO) trong `src/types/post.types.ts`
> *Lưu ý: Đảm bảo tên trường khớp với DTO trả về từ Backend ASP.NET Core.*
```typescript
export interface PostDto {
  id: string;
  authorName: string;
  authorAvatar?: string;
  content: string;
  imageUrl?: string;
  likesCount: number;
  commentsCount: number;
  createdAt: string;
}
```

### Bước 2: Viết Service gọi API trong `src/services/post.service.ts`
```typescript
import { axiosClient } from "@/lib/api/axiosClient";
import { PostDto } from "@/types/post.types";

export const postService = {
  getFeed: async (page = 1, pageSize = 10): Promise<PostDto[]> => {
    return await axiosClient.get("/posts/feed", {
      params: { page, pageSize }
    });
  },

  createPost: async (content: string, image?: File): Promise<PostDto> => {
    const formData = new FormData();
    formData.append("content", content);
    if (image) formData.append("image", image);

    return await axiosClient.post("/posts", formData, {
      headers: { "Content-Type": "multipart/form-data" }
    });
  }
};
```

### Bước 3: Tạo Component hiển thị trong `src/components/features/posts/PostCard.tsx`
```tsx
import { PostDto } from "@/types/post.types";
import { Card, CardHeader, CardContent, CardFooter } from "@/components/ui/card";
import { Avatar, AvatarFallback, AvatarImage } from "@/components/ui/avatar";
import { Heart, MessageCircle, Share2 } from "lucide-react";

interface Props {
  post: PostDto;
}

export const PostCard = ({ post }: Props) => {
  return (
    <Card className="mb-4">
      <CardHeader className="flex flex-row items-center gap-3">
        <Avatar>
          <AvatarImage src={post.authorAvatar} />
          <AvatarFallback>{post.authorName[0]}</AvatarFallback>
        </Avatar>
        <div>
          <h4 className="font-semibold text-sm">{post.authorName}</h4>
          <span className="text-xs text-muted-foreground">{new Date(post.createdAt).toLocaleDateString()}</span>
        </div>
      </CardHeader>
      <CardContent>
        <p className="text-sm">{post.content}</p>
        {post.imageUrl && (
          <img src={post.imageUrl} alt="post" className="mt-3 rounded-lg w-full object-cover max-h-96" />
        )}
      </CardContent>
      <CardFooter className="flex gap-4 text-muted-foreground text-sm">
        <button className="flex items-center gap-1 hover:text-red-500 transition">
          <Heart className="w-4 h-4" /> {post.likesCount}
        </button>
        <button className="flex items-center gap-1 hover:text-blue-500 transition">
          <MessageCircle className="w-4 h-4" /> {post.commentsCount}
        </button>
      </CardFooter>
    </Card>
  );
};
```

### Bước 4: Nhúng vào Trang trong `src/app/(main)/feed/page.tsx`
```tsx
"use client";

import { useEffect, useState } from "react";
import { postService } from "@/services/post.service";
import { PostDto } from "@/types/post.types";
import { PostCard } from "@/components/features/posts/PostCard";

export default function FeedPage() {
  const [posts, setPosts] = useState<PostDto[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    postService.getFeed()
      .then(setPosts)
      .finally(() => setLoading(false));
  }, []);

  if (loading) return <div>Đang tải bài viết...</div>;

  return (
    <div className="max-w-xl mx-auto py-6">
      {posts.map((post) => (
        <PostCard key={post.id} post={post} />
      ))}
    </div>
  );
}
```

---

##  6. Các Quy Tắc Quan Trọng Cần Ghi Nhớ (Coding Standards)

1. **Phân biệt Server Component vs Client Component:**
   * Trong Next.js App Router, mặc định mọi file là **Server Component**.
   * Chỉ thêm dòng `"use client";` ở đầu file khi component đó:
     * Dùng React Hooks (`useState`, `useEffect`, `useRef`...)
     * Bắt sự kiện người dùng (`onClick`, `onChange`, `onSubmit`...)
     * Truy cập API trình duyệt (`window`, `localStorage`...)

2. **Quy tắc đặt tên:**
   * **Component file:** Viết theo kiểu `PascalCase` (vd: `PostCard.tsx`, `LoginForm.tsx`).
   * **Thư mục route:** Viết theo kiểu `kebab-case` chữ thường (vd: `reset-password`, `user-profile`).
   * **Hàm, biến, service:** Viết theo kiểu `camelCase` (vd: `getUserProfile`, `isSubmitting`).

3. **Không gọi API trực tiếp trong UI component bằng `fetch` trần:**
   * Luôn gọi qua các hàm trong thư mục `src/services/` để tận dụng axios instance đã cấu hình sẵn JWT Token và xử lý lỗi tập trung.

4. **Git Workflow:**
   * Không commit file `.env.local` và thư mục `node_modules` hoặc `.next/`.
   * Luôn tạo branch riêng cho từng tính năng (vd: `feature/feed-ui`, `feature/auth-login`) trước khi tạo Pull Request vào nhánh `main` hoặc `develop`.
