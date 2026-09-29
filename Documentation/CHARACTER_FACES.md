# 분리형 눈·입

![가벼운 평상시 깜빡임](CharacterRelaxedBlink.gif)

![평상시와 피격 표정 비교](CharacterFacesPreview.png)

![눈동자 마스킹과 4족형 이동 중 얼굴 표시](CharacterPupilMaskPreview.png)

플레이어 41종과 적 69종의 머리 PNG/PSB에서 기존 눈·입을 제거했습니다. 4족형 12종과 날개형 15종은 돌출된 주둥이가 없는 둥근 얼굴로 정리했습니다. 머리 외 레이어의 픽셀과 프레임·이름·레이어 ID는 보존했습니다. 동료 원본은 유지합니다.

## 프리팹에서 위치 조절

`Assets/DoodleIdle/CharacterRigs/Prefabs`의 프리팹을 열고 머리 본 아래 `Face`를 편집합니다. 본 계층은 프리팹마다 다를 수 있습니다.

- `Face`: 눈과 입 전체의 위치·회전·크기.
- `Face/LeftEye`, `Face/RightEye`: 각각의 눈 위치·회전·크기.
- 각 눈의 `EyelidMotion/White`: 흰자 렌더러. 평상시/피격 스프라이트는 `CharacterFace`에서 지정합니다. `EyelidMotion`은 코드가 깜빡임을 처리하는 전용 오브젝트입니다.
- 적의 각 눈 아래 `Brow`: 사나운 눈매를 만드는 별도 눈썹입니다. 위치·크기를 따로 조절할 수 있습니다.
- 각 눈의 `PupilMotion/Pupil`: 눈동자 이미지의 크기·위치. `PupilMotion`은 코드가 시선을 움직이는 전용 오브젝트입니다.
- 각 눈의 `EyelidMotion/White/PupilMask`: 흰자 안쪽에서 눈동자를 잘라 주는 SpriteMask입니다. 흰자 크기·위치와 눈꺼풀을 따라가며, 각 눈의 SortingGroup으로 다른 눈/캐릭터와 마스크가 섞이지 않습니다.
- `Face/Mouth`: 입의 위치·회전·크기.

외형을 교체해도 이 좌표를 덮어쓰지 않습니다. 같은 타입의 적들은 같은 프리팹 얼굴 배치를 공유합니다. 동료 외형으로 교체하면 분리형 얼굴은 꺼집니다.

`CharacterFace`의 `Hurt Duration`으로 피격 표정 유지 시간, 각 눈의 `Travel`로 눈동자 이동 한계, `Gaze Speed`로 시선 이동 속도를 조정합니다. `Target`에는 추적할 Transform을 넣습니다. 게임에서는 플레이어가 가까운 전투 타겟, 적이 플레이어의 얼굴을 바라보도록 자동 연결합니다.

눈동자는 흰자 가장자리까지 이동하고, 밖으로 나간 부분은 마스킹됩니다. `Pupil` 크기와 `Travel`을 조정해도 눈 테두리 밖으로 그려지지 않습니다. 얼굴 전체의 SortingGroup은 매 프레임 머리 렌더러의 레이어와 순서+`Sorting Offset`을 따라갑니다. 따라서 4족형 이동처럼 머리 순서가 바뀌는 기존 애니메이션에서도 눈·입이 머리 뒤로 숨지 않습니다. 머리/몸통/팔다리의 기존 순서는 변경하지 않습니다.

## 동작

평상시에는 원형 흰자·원형 검은 눈동자를 사용하며, 적에게는 별도 눈썹으로 사나운 눈매를 적용합니다. 입 주변의 다른 색 패치나 중앙 장식 때문에 눈·입 배치가 제한되던 머리 12종도 정리했습니다.

평상시 깜빡임은 둥근 눈의 눈꺼풀이 빠르게 내려갔다 부드럽게 다시 올라오는 동작입니다. 감은 눈 그림으로 바뀌거나 감은 표정으로 멈춰 있지 않습니다. 눈동자는 원형을 유지하며 좁아지는 눈꺼풀 마스크에 가려집니다. 입과 눈썹은 그대로이고, 질끈 감는 표정은 피격 때만 사용합니다.

피격하지 않아도 2.5~5.5초의 불규칙한 간격으로 0.13초 동안 눈을 깜빡입니다. `Blinking`, `Blink Interval`, `Blink Duration`으로 조절합니다. 깜빡이는 동안 입은 그대로이며, 피격하면 깜빡임을 취소하고 피격 눈·입을 우선 표시합니다. 공격 중 받은 피해에도 얼굴은 반응합니다. 일시정지 중에는 표정 시간과 시선 이동이 멈추고, 풀에 반환하면 표정과 타겟을 초기화합니다. 얼굴 렌더러와 눈썹에도 기존 피격 틴트와 투명도를 적용합니다. 깜빡임은 전투 난수에 영향을 주지 않습니다.

모든 얼굴 파츠는 기존 머리 본을 따라갑니다. 별도의 Animator 레이어나 애니메이션 곡선을 추가하지 않습니다. 기존 `.anim`, `.controller`, `.mask` 파일을 변경하지 않고 기존 본 자세를 유지한 채 스킨 메시를 다시 생성했습니다.

## 에셋과 갱신

분리된 스프라이트는 `Assets/DoodleIdle/Art/CharacterSprites/FaceParts`에 있습니다. 머리 원본은 기존 PNG/PSB 경로를 유지합니다. 이미지 생성은 내장 ImageGen으로 수행했으며 생성 지시문은 `Assets/DoodleIdle/CharacterRigs/Reports/face_generation_prompts.json`에 기록했습니다.

원형 눈·적 눈썹과 머리 색 패치 수정 지시문은 같은 폴더의 `face_refinement_prompts.json`에 기록했습니다.

Unity 메뉴 `Doodle Idle/Character Rigs/Install Separated Faces (Keep Animation)`는 얼굴 컴포넌트를 연결합니다. 이미 있는 얼굴의 위치는 보존합니다. `Render Separated Face Portraits`는 얼굴을 포함한 UI 초상화를 갱신합니다.

PSB 그림을 다시 수정했다면 기존 `Rebuild Selected Skins Keep Prefab Poses`를 사용하세요. **`Build All`과 `Rebuild Prefabs Only`는 애니메이션과 프리팹을 재생성하므로 이 작업에 사용하지 않았습니다.**
