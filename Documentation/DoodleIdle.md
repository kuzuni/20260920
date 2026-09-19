# 낙서 원정대 · Unity 2D 방치형 프로토타입

Unity 6000.3.8f1 프로젝트입니다. `Assets/DoodleIdle/DoodleIdle.unity`를 열고 Play를 누르면 자동 전투가 시작됩니다. 씬이 아직 없다면 **Doodle Idle → Create or Open Prototype** 메뉴로 생성합니다.

## 구현 범위

- 머리와 떠 있는 방망이만 있는 플레이어. 직접 생성한 참조 그림체의 스프라이트 사용.
- 직접 생성한 베이지색 흙바닥. 위에서 내려다보는 바닥만 표시하며, 인접 타일을 반전하여 경계가 이어집니다.
- 뿔 달린 적 3종, 시작 80마리. 20마리 미만이 되는 즉시 살아 있는 적을 포함해 80마리까지 보충.
- 플레이어와 적: 중력 없는 Dynamic Rigidbody2D + CircleCollider2D. 적끼리 겹치지 않게 초기 위치 검사 및 물리 충돌. 바위에는 고정 콜라이더, 지도 가장자리에는 보이지 않는 경계 콜라이더 적용.
- 가까운 적을 향해 자동 이동, 0.65초마다 방망이를 휘둘러 전방 검기를 발사.
- 5초마다 가급적 4~9유닛 떨어진 적에게 대시. 이동 경로를 쓸어 검사하여 적을 타격하고 잔상 생성.
- 바나나 5개가 항상 플레이어 주위를 공전. 적별 0.35초 재피격 간격.
- 3.2초마다 가장 가까운 서로 다른 적 최대 3명에게 돌멩이를 하나씩 포물선으로 발사.
- 피격 틴트, 먼지, 대시 잔상, 처치/생존/보충 횟수 및 쿨다운 표시.

전투와 충돌을 검증하기 위한 최소 샌드박스이며, 플레이어 사망/성장/저장/상점은 포함하지 않습니다. 기본적으로 자동 전투가 계속됩니다.

## 조작

| 입력 | 동작 |
| --- | --- |
| Space / 일시정지 버튼 | 일시정지 또는 계속 |
| R / 다시 시작 버튼 | 적 80마리와 초기 스킬 상태로 재시작 |
| Tab | 자동/수동 이동 전환 |
| WASD / 방향키 | 수동 모드에서 이동; 공격과 스킬은 계속 자동 |

`Doodle Idle Prototype` 오브젝트의 `DoodleIdleGame` Inspector에서 적 수, 보충 임계값, 맵 크기, 이동 속도, 스킬 간격을 조정할 수 있습니다.

## 검증 및 빌드

- **GitHub Actions → Doodle Idle Unity tests**: 서버의 PlayMode 테스트가 실제 물리 충돌 및 120초 자동 전투를 검사합니다. 결과 XML과 Unity 로그는 Actions 아티팩트로 저장합니다. 설정은 `Documentation/CI.md` 참조.
- **Doodle Idle → Build Windows Prototype**: `Builds/DoodleIdle/DoodleIdle.exe` 생성.

## 에셋

- `Assets/DoodleIdle/Resources/DoodleIdle/Characters.png`: image_gen 기본 도구로 생성한 투명 3×3 아틀라스. 플레이어, 적 3종, 방망이, 바나나, 돌멩이, 바위, 풀.
- `Assets/DoodleIdle/Resources/DoodleIdle/Dirt.png`: image_gen 기본 도구로 별도 생성한 흙바닥.
- 원본 생성 이미지의 알파를 그대로 보존하며 Unity에서 셀별 알파 경계에 맞춰 Sprite로 분할합니다.
- 검기, 먼지, 그림자는 단순한 런타임 효과 텍스처입니다.
- 한글 UI: Google Fonts의 Nanum Pen Script. SIL Open Font License는 `Font-LICENSE.txt`에 동봉.
- 이미지 생성에 사용한 최종 프롬프트는 `image-prompts.md` 참조.
