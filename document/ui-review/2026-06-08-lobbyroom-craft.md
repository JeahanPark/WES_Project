# UI 전수 점검 — LobbyRoomPopup / CraftPopup (디자인 렌즈)

- 일시: 2026-06-08
- 점검자: designer (ui-audit 팀)
- 방법: 릴레이 호스트 룸 진입 → 합성 렌더 캡처(u_screenshot 480p + wesqa 1920x1080 크롭) + wesqa 런타임 RectTransform 정밀 측정
- 렌즈: 레이아웃 정밀 / 렌더 진위 / 색·대비·가독 / 자산 적용 완료성 (기능·플로우는 QA 영역)

진입 경로: 타이틀 StartButton → 로비 RoomCreateButton(Create) → 룸 진입 OK

---

## 과제 A — LobbyRoomPopup

트리: `LobbyRoomPopup/{BG, TitleText, RoomCodeText, InviteButton, Player1~4Text, StartGameButton, Chat/{BG, Scroll View/Viewport/Content, InputField(TMP)/...}}`

### 정밀 측정 (정규화 pos / size)
| 요소 | pos | size | 판정 |
|---|---|---|---|
| TitleText "Room" | 0.5, 0.046 | 0.21, 0.056 | OK 상단중앙 |
| RoomCodeText "ROOM CODE : FF9C9F" | 0.5, 0.120 | 0.26, 0.046 | OK |
| InviteButton | 0.5, 0.185 | 0.10, 0.046 | OK |
| Player1~4Text | 0.5, 0.454/0.500/0.546/0.593 | 0.21, 0.037 | OK 간격 0.046 균등 |
| StartGameButton | 0.5, 0.926 | 0.156, 0.056 | OK 하단중앙 |
| Chat/Scroll View | 0.820, 0.370 | 0.360, 0.739 | OK |
| Chat/InputField | 0.818, 0.956 | 0.345, 0.059 | 위치이슈(결함A-1) |

### 요소별 판정
| 요소 | 통과/결함 | 메모 | 캡처 |
|---|---|---|---|
| Room 제목 | 통과 | 흰 텍스트, 대비 양호 | room_full.png |
| ROOM CODE | 통과 | 가독 양호 | room_full.png |
| Invite 버튼 | 통과 | 나무 9-slice + 흰 텍스트(저해상 캡처에선 평면처럼 보였으나 고해상 크롭상 정상) | room_invite_crop.png |
| Player1~4 슬롯 | 통과 | 흰 텍스트, 어두운 BG 대비 양호, 간격 균등 | room_players_crop.png |
| START GAME | 통과 | 나무 9-slice + 흰 볼드, 대비 양호 | room_start_crop.png |
| 좌측 나무 프레임(BG) | 결함 A-3 (낮음) | 우측 채팅 프레임 대비 과도하게 어두워 디테일 손실(좌우 밝기 불일치). BG 풀스크린 액자 이미지 명암 문제 | room_left_crop.png |
| 채팅 입력창(InputField) | 결함 A-1 (높음) | placeholder "Enter text..." 안 보임(어두운 회색 위 어두운 회색=저대비). 입력창이 채팅 나무 프레임 바깥 하단에 분리되어 채팅 패널과 동떨어져 보임 | room_input_crop.png |
| 채팅 패널 영역 구분 | 결함 A-2 (중) | Scroll View(메시지영역)와 InputField 경계 없음. 프레임 안쪽이 균일 나무텍스처라 "어디에 채팅이 뜨고 어디에 입력하는지" 시각 구분 불가 | room_chat_crop.png |

### 결함 요약 (A)
- **A-1 (높음)** 채팅 입력창 placeholder 저대비 + 입력창이 채팅 프레임과 분리 → 입력 위치 불명. 시각수정 가능: InputField BG를 채팅 프레임 안쪽으로 이동/편입 + placeholder 색 밝게.
- **A-2 (중)** 채팅 메시지영역/입력영역 시각 경계 부재. 시각수정 가능: 입력창에 별도 sunken BG, 메시지영역과 구분선.
- **A-3 (낮음)** BG 좌측 영역 과암. 의도 가능성(액자 명암) — director 톤 확인 후 결정.

렌더 진위: 체커·워터마크·평면 placeholder 없음 (모든 프레임 정상 자산).

---

## 과제 B — CraftPopup

트리: `CraftPopup/PopupPanel/{Header/{TitleText, CloseButton}, TabRow/{BuildingTabButton, ItemTabButton}, LeftPanel/CraftScroll/Viewport/Content/Cell[n]/{FrameBg, IconImage, NameText, UnlockFlashFrame}, DetailPanel/{IconImage, NameText, DescriptionText, MaterialsLabel, MaterialsContainer, ConditionsContainer}, HintText}`

진입: 인게임 진입 후 CraftHUDTab/BuildingButton 클릭

### 정밀 측정
| 요소 | pos | size | 판정 |
|---|---|---|---|
| PopupPanel | 0.5, 0.5 | 0.469, 0.574 | OK 중앙 |
| TitleText "제작" | 0.354, 0.236 | — | OK 좌상단 |
| CloseButton | 0.719, 0.236 | 0.021, 0.037 | OK 우상단 |
| BuildingTabButton | 0.297, 0.282 | 0.0625, 0.046 | OK 활성 |
| ItemTabButton | 0.365, 0.282 | 0.0625, 0.046 | OK 비활성 |
| LeftPanel(셀그리드) | 0.339, 0.546 | 0.147, 0.482 | OK |
| Cell[0] / Cell[1] | 0.292 / 0.349, 0.352 | 0.052, 0.093 | 같은행 y일치, 간격 0.057 균등 OK |
| DetailPanel | 0.577, 0.546 | 0.316, 0.482 | OK 우측 |

### 요소별 판정
| 요소 | 통과/결함 | 메모 | 캡처 |
|---|---|---|---|
| 제목 "제작" | 통과 | 흰 텍스트 가독 양호 | craft_header_crop.png |
| 닫기버튼 x | 통과 | 밝은 회색 프레임, 식별 양호 | craft_header_crop.png |
| 탭(건물/아이템) | 통과 | 활성=황금강조, 비활성=회색, 상태구분 양호 | craft_header_crop.png |
| 레시피 셀 그리드 | 통과 | 나무 9-slice + 아이콘(모닥불/햇불) + 이름. 셀이 세로로 약간 길지만 아이콘+이름 레이아웃 적합. 간격 균등 | craft_cells_crop.png |
| 우측 상세 패널 | 통과 | "필요 자원" 라벨 + "제작할 항목을 선택하세요" 안내, 대비 양호 | craft_detail_crop.png |
| 스크롤 | 통과(미검증 깊이) | Content size 측정됨, 스크롤 동작은 QA 영역 | — |

### 결함 요약 (B)
- 디자인 렌즈 결함 없음(통과). 
- 관찰(보류, 기능/디렉터 영역): 항목 미선택 상태에서 "필요 자원" 라벨이 빈 채 노출 — 어색할 수 있으나 선택 시 자연스러워질 가능성. 기능 흐름이라 디자인 판정 보류.

렌더 진위: 체커·워터마크·평면 placeholder 없음.

---

## 과제 C — 인게임 흐릿함 1차 관찰 (client 협업, 검증 미완)

wesqa 트리에서 풀스크린(pos 0.5,0.5 / size 1.0,1.0) Image 다중 적층 확인, 전부 visible=True:
- `CoreTensionOverlay/{DayNightTint, AmbientFog, ColdOverlay1, ColdOverlay2, ColdOverlay3, HpVignette, DeathOverlay}`
- `NightVisionRoot/DarknessOverlay`

총 7~8장 풀스크린 오버레이 겹침 = 흐릿함 구조 원인 후보. 유력: AmbientFog + ColdOverlay 3중첩.

검증 미완: 디자이너는 플레이모드 GameObject 토글 권한 없음(u_editor set_active 플레이모드 불가, wesqa invoke는 QA영역). client에게 토글 훅 의뢰함 → client가 레이어별 SetActive 토글 제공하면 재진입 캡처로 주범 특정 예정. **현재 BLOCKED(client 응답·에디터 재점유 대기).**

캡처: captures/ingame_blur_01.png

---

## 캡처 인덱스 (모두 c:/GitFork/WES_Project/document/ui-review/captures/)
- title.png, lobby.png, room_full.png, room_chat_crop.png, room_invite_crop.png, room_input_crop.png, room_left_crop.png, room_players_crop.png, room_start_crop.png
- craft_full.png, craft_cells_crop.png, craft_header_crop.png, craft_detail_crop.png
- ingame_blur_01.png
