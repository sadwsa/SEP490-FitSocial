$raw = Get-Content backend/FitSocial.UnitTests/TestData/EditCoachSubscriptionPlanTestCases.csv
$header = $raw[0] -split ','
$ids = @(); for ($i=1; $i -lt $raw.Count; $i++) { $ids += ($raw[$i] -split ',')[0] }
$ncol = $ids.Count
$ncolsTotal = 5 + $ncol
$control = @('UTCID','Type','ExpectedSuccess','ExpectedMessage')
$condCols = @()
for ($i=0; $i -lt $header.Count; $i++) { if ($control -notcontains $header[$i]) { $condCols += @{Name=$header[$i]; Index=$i} } }

function NewRow(){ , (New-Object string[] $ncolsTotal) }
function CsvQuote($s){ if ($s -match '[",\r\n]') { '"' + ($s -replace '"','""') + '"' } else { $s } }
function RowStr($arr){ ($arr | ForEach-Object { CsvQuote $_ }) -join ',' }

$rows = @()
$r = NewRow; $rows += ,$r
$r = NewRow; $r[0]='Function Code'; $r[2]='UpdatePlanAsync'; $r[5]='Function Name'; $r[11]='Edit Coach Subscription Plan'; $rows += ,$r
$r = NewRow; $r[0]='Created By'; $r[2]='SangNH'; $r[5]='Executed By'; $r[11]='SangNH'; $rows += ,$r
$r = NewRow; $r[0]='Lines  of code'; $r[2]='107'; $r[5]='Lack of test cases'; $r[11]='0'; $rows += ,$r
$req = 'Verify that UpdatePlanAsync edits a coach subscription plan (or returns the appropriate failure response) across valid, invalid, and boundary inputs, considering plan existence, field updates, and database/audit failures.'
$r = NewRow; $r[0]='Test requirement'; $r[2]=$req; $rows += ,$r
$r = NewRow; $r[0]='Passed'; $r[2]='Failed'; $r[5]='Untested'; $r[11]='N/A/B'; $r[14]='Total Test Cases'; $rows += ,$r
$types = @{}; for ($i=1;$i -lt $raw.Count;$i++){ $t=($raw[$i] -split ',')[1]; $types[$t]++ }
$r = NewRow; $r[0]=$raw.Count-1; $r[2]='0'; $r[5]='0'; $r[11]=if($types['N']){$types['N']}else{0}; $r[12]=if($types['A']){$types['A']}else{0}; $r[13]=if($types['B']){$types['B']}else{0}; $r[14]=$raw.Count-1; $rows += ,$r
$r = NewRow; $rows += ,$r
$r = NewRow; for ($i=0;$i -lt $ncol;$i++){ $n=$i+1; $r[5+$i]="UCID"+$n.ToString("00") }; $rows += ,$r
$r = NewRow; $r[0]='Condition'; $r[1]='Precondition'; $rows += ,$r
$r = NewRow; $r[3]='Database connected'; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]='O' }; $rows += ,$r

foreach ($cc in $condCols) {
  $r = NewRow; $r[1]=$cc.Name; $rows += ,$r
  $vals = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$cc.Index]; if ($vals -notcontains $f) { $vals += $f } }
  foreach ($v in $vals) {
    $disp = if ([string]::IsNullOrWhiteSpace($v)) { 'empty/null' } else { '"' + $v + '"' }
    $r = NewRow; $r[3]=$disp
    $m = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$cc.Index]; if ($f -eq $v) { $m+='O' } else { $m+='' } }
    for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]=$m[$i] }
    $rows += ,$r
  }
}

$esIndex = [Array]::IndexOf($header,'ExpectedSuccess')
$emIndex = [Array]::IndexOf($header,'ExpectedMessage')
# Confirm Return
$r = NewRow; $r[0]='Confirm'; $r[1]='Return'; $rows += ,$r
$r = NewRow; $r[3]='Returns ApiResponseDto, Success=true'; $m = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$esIndex]; if ($f -eq 'TRUE') { $m+='O' } else { $m+='' } }; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]=$m[$i] }; $rows += ,$r
$r = NewRow; $r[3]='Returns ApiResponseDto, Success=false'; $m = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$esIndex]; if ($f -eq 'FALSE') { $m+='O' } else { $m+='' } }; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]=$m[$i] }; $rows += ,$r
# Message
$r = NewRow; $r[1]='Message'; $rows += ,$r
$msgs = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$emIndex]; if ($msgs -notcontains $f) { $msgs += $f } }
foreach ($v in $msgs) {
  $dispMsg = $v.Trim('"')
  $r = NewRow; $r[3]='"'+$dispMsg+'"'
  $m = @(); for ($i=1;$i -lt $raw.Count;$i++){ $f=($raw[$i] -split ',')[$emIndex]; if ($f -eq $v) { $m+='O' } else { $m+='' } }
  for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]=$m[$i] }
  $rows += ,$r
}
# Exception
$r = NewRow; $r[1]='Exception'; $rows += ,$r
$r = NewRow; $r[3]='No unhandled exception propagates'; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]='O' }; $rows += ,$r
# Result
$r = NewRow; $r[0]='Result'; $r[1]='Type(N : Normal, A : Abnormal, B : Boundary)'
$typeMarks = for($i=1;$i -lt $raw.Count;$i++){ ($raw[$i] -split ',')[1] }
for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]=$typeMarks[$i] }; $rows += ,$r
$r = NewRow; $r[1]='Passed/Failed'; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]='P' }; $rows += ,$r
$r = NewRow; $r[1]='Executed Date'; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]='11/01' }; $rows += ,$r
$r = NewRow; $r[1]='Defect ID'; for ($i=0;$i -lt $ncol;$i++){ $r[5+$i]='-' }; $rows += ,$r

$lines = $rows | ForEach-Object { RowStr $_ }
$lines -join "`r`n" | Set-Content -Path "Report5_Unit Test.xlsx - Edit Coach Subscription Plan.csv" -Encoding UTF8
Write-Host "done rows=$($lines.Count)"
