# WES QA Poco 포크 — M3 (시각 검증: Screenshot + aircv + 리포트) 구현 플랜

> **For agentic workers:** REQUIRED SUB-SKILL: superpowers:subagent-driven-development. 체크박스(`- [ ]`) 추적.

**Goal:** wesqa가 화면을 캡처하고, 이미지 템플릿 매칭으로 "이게 화면에 보이나"를 결정적으로 단언하며, 스텝별 스크린샷이 박힌 HTML 리포트를 생성한다.

**Architecture:** C# `Screenshotter`가 `ScreenCapture`로 프레임을 캡처→리사이즈→JPG→base64로 `Screenshot(width)` RPC에 응답. Python은 Poco `StdScreen`이 받은 base64를 numpy 이미지로 디코드하고, Airtest에서 추출한 `aircv`(template matching)로 매칭한다. `wesqa.report`가 스텝·스크린샷을 HTML로 묶는다.

**Tech Stack:** Unity 6 C# (ScreenCapture, RenderTexture, EncodeToJPG), Python (numpy, opencv-python, Airtest `aircv` 추출본). 검증 = MCP 플레이모드 + Python.

**Screenshot 계약(Poco `StdScreen` 검증):** `Screenshot(width:int)` → `[base64, fmt]`. fmt가 `.deflate`로 끝나면 클라가 zlib 해제. JPG는 이미 압축이라 `fmt="jpg"`(deflate 미사용).

---

## File Structure

- Create: `Assets/WesQA/Runtime/Screenshotter.cs` — 캡처·리사이즈·JPG·base64
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs` — `Screenshot` 디스패치
- Create: `tools/wesqa/aircv/` — Airtest aircv 추출(template matching)
- Create: `tools/wesqa/wesqa/vision.py` — 스크린샷 디코드 + 템플릿 매칭 헬퍼
- Modify: `tools/wesqa/wesqa/__init__.py` — `WesPoco.screenshot()` 추가
- Create: `tools/wesqa/wesqa/report.py` — 경량 HTML 리포트
- Modify: `tools/wesqa/requirements.txt` (없으면 생성) — numpy, opencv-python

---

### Task 1: C# Screenshot RPC

**Files:**
- Create: `Assets/WesQA/Runtime/Screenshotter.cs`
- Modify: `Assets/WesQA/Runtime/RpcMethods.cs`

- [ ] **Step 1: Screenshotter 작성**

`Assets/WesQA/Runtime/Screenshotter.cs`:
```csharp
using System;
using UnityEngine;

namespace WesQA
{
    /// <summary>화면을 캡처→요청 width로 리사이즈→JPG→base64. 메인스레드(서버 펌프)에서 호출됨.</summary>
    public static class Screenshotter
    {
        // 반환: [base64Jpg, "jpg"] (Poco StdScreen 계약). width<=0 또는 >=원본이면 원본 크기.
        public static object[] Capture(int width)
        {
            var src = ScreenCapture.CaptureScreenshotAsTexture();
            try
            {
                Texture2D outTex = src;
                bool resized = false;
                if (width > 0 && width < src.width)
                {
                    int height = Mathf.Max(1, Mathf.RoundToInt(src.height * (width / (float)src.width)));
                    var rt = RenderTexture.GetTemporary(width, height);
                    Graphics.Blit(src, rt);
                    var prev = RenderTexture.active;
                    RenderTexture.active = rt;
                    outTex = new Texture2D(width, height, TextureFormat.RGB24, false);
                    outTex.ReadPixels(new Rect(0, 0, width, height), 0, 0);
                    outTex.Apply();
                    RenderTexture.active = prev;
                    RenderTexture.ReleaseTemporary(rt);
                    resized = true;
                }
                byte[] jpg = outTex.EncodeToJPG(80);
                if (resized) UnityEngine.Object.Destroy(outTex);
                return new object[] { Convert.ToBase64String(jpg), "jpg" };
            }
            finally
            {
                UnityEngine.Object.Destroy(src);
            }
        }
    }
}
```

- [ ] **Step 2: RpcMethods에 Screenshot 디스패치**

`Assets/WesQA/Runtime/RpcMethods.cs`의 `Invoke` switch에 case 추가(`Dump` 근처):
```csharp
                case "Screenshot":
                {
                    var a = req.Args();
                    int w = a.Count > 0 ? a[0].ToObject<int>() : 0;
                    return Screenshotter.Capture(w);
                }
```

- [ ] **Step 3: 컴파일 확인**

`u_editor_asset(refresh)` → 6초 → `u_console(error)` 에러 0.

- [ ] **Step 4: 라이브 검증 — 스크린샷 저장**

`u_play(enter)` → 5초 → Run:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "
import sys,base64; sys.path.insert(0,'.')
from wesqa import WesPoco
g=WesPoco(instance=0)
b64,fmt = g.agent.screen.getScreen(640)
data = base64.b64decode(b64)
open('bench/_shot.jpg','wb').write(data)
import os; print('fmt',fmt,'bytes',os.path.getsize('bench/_shot.jpg'))
" 2>&1 | grep -v '\[rpc\]'
```
Expected: `fmt jpg bytes <수천 이상>` (검은/빈 이미지면 수백 바이트 — 실패). `bench/_shot.jpg`가 실제 게임 화면이어야 함. `u_play(exit)`. (캡처가 검정이면 `ScreenCapture.CaptureScreenshotAsTexture` 타이밍 이슈 → 카메라 RenderTexture 렌더 방식으로 대체.)

- [ ] **Step 5: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/Assets/WesQA/Runtime/Screenshotter.cs WES/Assets/WesQA/Runtime/RpcMethods.cs
git commit -m "WesQA: Screenshot RPC(캡처→리사이즈→JPG→base64)"
```

---

### Task 2: aircv 추출 + 템플릿 매칭 헬퍼

**Files:**
- Create: `tools/wesqa/aircv/` (Airtest aircv 복사)
- Create: `tools/wesqa/wesqa/vision.py`
- Modify: `tools/wesqa/wesqa/__init__.py`
- Create: `tools/wesqa/requirements.txt`

- [ ] **Step 1: 의존성 설치 + 기록**

`tools/wesqa/requirements.txt` 생성:
```
numpy
opencv-python
```
설치:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -m pip install -r requirements.txt 2>&1 | tail -3
python -c "import cv2, numpy; print('cv2', cv2.__version__)"
```
Expected: cv2 버전 출력.

- [ ] **Step 2: Airtest aircv 복사**

```powershell
$src = "C:\Users\cgq02\Downloads\Airtest-master\Airtest-master\airtest\aircv"
$dst = "c:\GitFork\WES_Project\WES\tools\wesqa\aircv"
Copy-Item -Recurse -Force $src $dst
Remove-Item -Force "$dst\screen_recorder.py" -ErrorAction SilentlyContinue
```
import 스모크:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "import sys; sys.path.insert(0,'.'); from aircv.template_matching import TemplateMatching; from aircv.cal_confidence import cal_ccoeff_confidence; print('OK')"
```
Expected: `OK`. (airtest.* 의존 import가 있으면 해당 줄을 찾아 aircv 내부 상대 import로 교정하거나 제거. `import cv2`/numpy만 의존하도록.)

- [ ] **Step 3: vision 헬퍼 작성**

`tools/wesqa/wesqa/vision.py`:
```python
# coding=utf-8
"""스크린샷 base64 → numpy 이미지, aircv 템플릿 매칭 헬퍼."""
import base64

import cv2
import numpy as np

from aircv.template_matching import TemplateMatching


def b64_to_image(b64):
    """base64(JPG) → BGR numpy 이미지."""
    if isinstance(b64, (bytes, bytearray)):
        raw = base64.b64decode(b64)
    else:
        raw = base64.b64decode(b64.encode("ascii") if isinstance(b64, str) else b64)
    arr = np.frombuffer(raw, dtype=np.uint8)
    return cv2.imdecode(arr, cv2.IMREAD_COLOR)


def find_template(screen_img, template_img, threshold=0.8):
    """screen 안에서 template를 찾는다. → dict(result=중심좌표, confidence,...) 또는 None."""
    match = TemplateMatching(template_img, screen_img, threshold=threshold)
    return match.find_best_result()


def template_on_screen(screen_img, template_path, threshold=0.8):
    tpl = cv2.imread(template_path, cv2.IMREAD_COLOR)
    if tpl is None:
        raise FileNotFoundError(template_path)
    return find_template(screen_img, tpl, threshold) is not None
```

- [ ] **Step 4: WesPoco.screenshot() 추가**

`tools/wesqa/wesqa/__init__.py`의 `WesPoco` 클래스에 메서드 추가:
```python
    def screenshot(self, path=None, width=0):
        """현재 화면 캡처 → (BGR numpy 이미지). path 주면 파일로도 저장."""
        from . import vision
        b64, _fmt = self.agent.screen.getScreen(width)
        img = vision.b64_to_image(b64)
        if path:
            import cv2
            cv2.imwrite(path, img)
        return img
```

- [ ] **Step 5: 라이브 검증 — 자기 자신 템플릿 매칭**

`u_play(enter)` → 5초 → Run (스크린샷에서 영역을 잘라 템플릿으로 만들고, 다시 찾기):
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "
import sys; sys.path.insert(0,'.')
import cv2
from wesqa import WesPoco
from wesqa import vision
g=WesPoco(instance=0)
img=g.screenshot('bench/_full.png')
h,w=img.shape[:2]
# 화면 중앙 영역을 템플릿으로 crop
crop=img[h//2-40:h//2+40, w//2-80:w//2+80]
cv2.imwrite('bench/_tpl.png', crop)
res=vision.find_template(img, crop, threshold=0.8)
print('match conf:', None if res is None else res.get('confidence'))
print('FOUND' if res else 'NOT-FOUND')
" 2>&1 | grep -v '\[rpc\]'
```
Expected: `match conf: ~1.0` + `FOUND`(자기 자신이라 confidence 매우 높음). `NOT-FOUND`면 aircv 인자순/이미지 디코드 문제 → 조사문서 `airtest-02`의 인자순 주의(함수형 vs 클래스형) 참고해 교정. `u_play(exit)`.

- [ ] **Step 6: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/tools/wesqa/aircv WES/tools/wesqa/wesqa/vision.py WES/tools/wesqa/wesqa/__init__.py WES/tools/wesqa/requirements.txt
git commit -m "wesqa: aircv 템플릿 매칭 추출 + 스크린샷 디코드/매칭 헬퍼"
```

---

### Task 3: 경량 HTML 리포트

**Files:**
- Create: `tools/wesqa/wesqa/report.py`
- Create: `tools/wesqa/tests/test_report.py`

- [ ] **Step 1: 실패 테스트 작성(리포트는 Unity 불필요 — 순수 단위 테스트)**

`tools/wesqa/tests/test_report.py`:
```python
# coding=utf-8
import os
from wesqa.report import Report


def test_report_writes_html(tmp_path):
    r = Report("스모크")
    r.step("로그인 화면", True)
    r.step("나무 3개", False, note="기대 3, 실제 2")
    out = os.path.join(str(tmp_path), "report.html")
    r.write(out)
    html = open(out, encoding="utf-8").read()
    assert "스모크" in html
    assert "로그인 화면" in html
    assert "PASS" in html and "FAIL" in html
    assert "기대 3, 실제 2" in html
```

- [ ] **Step 2: 실패 확인**

```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -m pytest tests/test_report.py -v
```
Expected: FAIL (`report` 모듈 없음).

- [ ] **Step 3: report 구현**

`tools/wesqa/wesqa/report.py`:
```python
# coding=utf-8
"""경량 HTML 리포트 — 스텝(pass/fail + 선택 스크린샷)을 한 파일로 묶는다."""
import base64
import html as _html
import time


class Report:
    def __init__(self, title):
        self.title = title
        self.steps = []

    def step(self, name, passed, note="", screenshot_b64=None):
        self.steps.append({
            "name": name, "passed": bool(passed),
            "note": note, "shot": screenshot_b64,
        })

    def write(self, path):
        rows = []
        for s in self.steps:
            badge = "PASS" if s["passed"] else "FAIL"
            color = "#1a7f37" if s["passed"] else "#cf222e"
            shot = ""
            if s["shot"]:
                shot = '<br><img style="max-width:480px;border:1px solid #ddd" '\
                       'src="data:image/jpeg;base64,{}">'.format(s["shot"])
            rows.append(
                '<tr><td><b style="color:{}">{}</b></td><td>{}</td>'
                '<td>{}{}</td></tr>'.format(
                    color, badge, _html.escape(s["name"]),
                    _html.escape(s["note"]), shot)
            )
        passed = sum(1 for s in self.steps if s["passed"])
        doc = (
            "<!doctype html><meta charset='utf-8'>"
            "<title>{title}</title>"
            "<body style='font-family:sans-serif;max-width:900px;margin:2em auto'>"
            "<h1>{title}</h1>"
            "<p>{p}/{n} PASS · {ts}</p>"
            "<table style='border-collapse:collapse' border='1' cellpadding='6'>"
            "<tr><th>결과</th><th>스텝</th><th>비고</th></tr>"
            "{rows}</table></body>"
        ).format(title=_html.escape(self.title), p=passed, n=len(self.steps),
                 ts=time.strftime("%Y-%m-%d %H:%M:%S"), rows="".join(rows))
        open(path, "w", encoding="utf-8").write(doc)
        return path
```

- [ ] **Step 4: 통과 확인**

```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -m pytest tests/ -q
```
Expected: 5 passed (기존 4 + report 1).

- [ ] **Step 5: 라이브 데모 — 스크린샷 박힌 리포트**

`u_play(enter)` → 5초 → Run:
```bash
cd /c/GitFork/WES_Project/WES/tools/wesqa && python -c "
import sys,base64; sys.path.insert(0,'.')
from wesqa import WesPoco
from wesqa.report import Report
g=WesPoco(instance=0)
b64,_=g.agent.screen.getScreen(640)
r=Report('wesqa 라이브 데모')
r.step('StartButton 존재', g('StartButton').exists(), screenshot_b64=b64)
r.write('bench/demo_report.html')
import os; print('report bytes', os.path.getsize('bench/demo_report.html'))
" 2>&1 | grep -v '\[rpc\]'
```
Expected: `report bytes <수만>`(스크린샷 base64 포함). `bench/demo_report.html`을 열면 스텝+화면 이미지. `u_play(exit)`.

- [ ] **Step 6: Commit**

```bash
cd /c/GitFork/WES_Project
git add WES/tools/wesqa/wesqa/report.py WES/tools/wesqa/tests/test_report.py
git commit -m "wesqa: 경량 HTML 리포트(스텝+스크린샷) + 단위 테스트"
```

---

## M3 완료 기준 (DoG)

- [ ] 컴파일 0, 스크린샷이 실제 게임 화면(검정 아님)
- [ ] aircv 자기-템플릿 매칭 confidence ~1.0 FOUND
- [ ] `pytest tests/` 5 passed
- [ ] 스크린샷 박힌 HTML 리포트 생성

## 다음

- **M4**: MPPM 멀티클라 포트 매핑 + `Invoke`/`SendMessage`→`Managers.Test` 브리지(=실 게임 seed 자동주입 → before/after 완전자동)
