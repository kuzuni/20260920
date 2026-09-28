# 게임·UI 캐릭터 통합

최종 PSB의 Appearance와 타입별 프리팹을 전투에 사용한다. `DoodleRigVisual`은 기존 충돌·타격·이동 좌표를 유지하며, 그 아래에 `CharacterRig` 프리팹을 생성한다. 머리 한 장을 흔드는 옛 캐릭터 렌더러는 표시하지 않는다.

- 플레이어: 기본형과 40개 테마, `Player_Standard.prefab`, 손 본에 연결된 근접 무기.
- 적: 69종, 일반2족·몸통있는날개·4족·머리두발·머리날개 프리팹. 배경 테마마다 해당 외형들을 순서대로 배치한다.
- 동료: 32종. 저장 ID와 전투 수치는 유지하고, 낮은 등급은 머리두발형, 높은 등급은 머리날개형을 사용한다.
- 자수정골렘: `companion_brick` 저장 ID를 유지하며 이름·외형·발사체·적중 조각을 자수정으로 바꿨다.

`DoodleCharacterCatalog`는 Resources의 `DoodleIdle/CharacterCatalog.asset`을 불러온다. 이 에셋이 각 Appearance, 프리팹, 본 기반으로 렌더한 UI 초상화를 직접 참조한다. `UiKit`, 스킨 초상화, 동료 슬롯과 프로필의 Player 아이콘은 같은 목록에서 이미지를 가져온다. UI의 옛 의상 합성 코드와 옛 동료 캐릭터 시트 로딩은 사용하지 않는다.

## 클라우드 생성과 검증

GitHub Actions `Doodle Idle Unity tests`에서 `validation: character-prefabs`로 실행한다. PlayMode 테스트의 prebuild 단계가 `DoodleCharacterCatalogBuild.Build`를 호출한다. 자수정골렘의 개별 메시를 갱신하고, 공통 본·기존 프리팹 자세를 보존하며, 142종의 Idle/Move 초상화와 런타임 목록을 생성한다. 로컬 Unity 실행은 필요 없다.

생성 에셋은 테스트 결과 아티팩트의 `character-assets/Assets`에, 실제 게임/UI 렌더는 `screenshots/prefab-*.png`에 저장된다. 검증 완료 후 생성 에셋을 원래 Assets 경로에 반영해야 일반 플레이어 빌드에서도 사용할 수 있다. 기존 씬의 수동 편집 내용은 별도로 보존한다.

스킨 목록은 기본형+40종으로 맞췄으며, 기존 20개 스킨의 구매·장착 ID와 가격/능력치는 유지한다. 추가 테마에는 별도 ID를 부여한다. `SkinAppearance_N_0`과 `SkinWeapon_N`의 N은 최종 플레이어 파일 번호에서 1을 뺀 값이다.

추가 20종은 2,100~4,000단계에서 해금된다. 프로필·내 PVP 순위·내 채팅·스탯·소환 결과의 플레이어 그림은 장착한 외형을 따른다. 스킨의 장착 표시는 얼굴을 가리지 않게 카드 상단에 배치한다.

씬에 직접 배치한 독립 CharacterRig는 에디터 확인용으로 유지하며, 게임을 시작하면 숨긴다. 전투에서는 DoodleRigVisual이 생성한 리깅 프리팹만 표시한다. 적 풀에서 다시 활성화할 때는 SpriteSkin의 OnEnable 이후에 외형을 교체한다.

로컬 검증도 같은 PlayMode 필터 `character-prefabs`를 사용한다. 생성된 CharacterCatalog와 RigPortraits는 Assets에 포함되므로 에디터나 플레이어 빌드가 별도 생성 절차 없이 읽을 수 있다.
