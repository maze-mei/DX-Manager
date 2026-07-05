# DX Manager 아이콘 적용 가이드 (코덱스용)

## 포함된 파일
- `DXManager.ico` — 멀티 레졸루션 아이콘 (16/20/24/32/48/256px 포함). **이 파일 하나만 있으면 충분합니다.**
- `DXManager_256.png` / `_48.png` / `_32.png` / `_16.png` — 개별 PNG (필요 시 About 창, 트레이 등에 개별 사용)
- `DXManager_source.svg` — 원본 벡터 소스 (추후 색상/디자인 수정 시 재사용)

## 1. 프로젝트에 아이콘 추가
1. `DXManager.ico`를 프로젝트 루트(예: `\Resources\` 폴더)에 복사
2. 솔루션 탐색기 → 프로젝트 우클릭 → **속성(Properties)** → **Application** 탭
3. **Icon and manifest** → **Icon** 항목에서 `DXManager.ico` 선택
   - 이렇게 하면 `.exe` 파일 자체의 아이콘(탐색기, 바탕화면 바로가기)이 설정됩니다.

## 2. 메인 폼(Form) 아이콘 설정
Form 디자이너에서:
- 폼 선택 → 속성 창(Properties) → **Icon** 속성에 `DXManager.ico` 지정

또는 코드로 직접 지정할 경우:
```csharp
this.Icon = new Icon("Resources/DXManager.ico");
```
(또는 리소스로 임베드했다면 `Properties.Resources.DXManager`)

## 3. 시스템 트레이 아이콘 (NotifyIcon)
`NotifyIcon` 컴포넌트를 쓰고 있다면:
```csharp
notifyIcon1.Icon = new Icon("Resources/DXManager.ico");
```
`.ico` 안에 16px 버전이 포함되어 있어서 트레이에서도 자동으로 적절한 해상도가 선택됩니다.

## 4. 리소스로 임베드하고 싶다면
1. `DXManager.ico`를 프로젝트에 추가 (우클릭 → 추가 → 기존 항목)
2. 파일 속성에서 **빌드 작업(Build Action)** → `Embedded Resource` 또는 `Content`로 설정
3. `Properties.Resources.resx`에 아이콘으로 추가하면 `Properties.Resources.DXManager`로 접근 가능

## 참고
- 배경 그라데이션: 보라(#8b7bf0) → 스카이블루(#4fd1e8), 대각선 방향
- 향후 이 아이콘의 색상 톤을 앱 내 강조색(버튼, 선택 상태 등)에도 맞추면 브랜드 일관성이 더 좋아집니다.
- 디자인을 다시 조정하고 싶으면 `DXManager_source.svg`를 Claude에게 다시 전달해 반영 가능합니다.
