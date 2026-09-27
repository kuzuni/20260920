# 공통 캐릭터 리그

PSB 원본은 `../Art/CharacterSprites/PSB`, 게임에서 사용할 프리팹은 `Prefabs`에 있습니다. 외형 데이터 118개는 `Appearances/Player`, `Appearances/Enemies`, `Appearances/Companions` 아래 타입별로 정리했습니다.

| 타입 | 부위 | 본 수 (Root 포함) | 프리팹 |
|---|---|---:|---|
| standard | 머리, 몸통, 팔1·2, 다리1·2 | 11 | Character_standard |
| wing | 머리, 몸통, 날개1·2, 다리1·2 | 11 | Character_wing |
| quad | 머리, 몸통, 다리1·2·3·4, 꼬리 | 13 | Character_quad |
| biped | 머리, 다리1·2 | 6 | Character_biped |
| floating | 머리, 날개1·2 | 6 | Character_floating |

`Player_Standard`는 standard와 동일한 골격에 별도 무기 렌더러를 추가한 플레이어용 프리팹입니다. 기본형은 경찰 테마의 근접 무기를 들며, 테마 외형으로 교체하면 해당 무기로 바뀝니다. 무기는 PSB의 본 개수에 포함하지 않습니다.

같은 타입의 PSB Character Skeleton은 본 이름·GUID·부모·위치·회전·길이가 정확히 같습니다. 머리·몸통은 각각 한 본, 팔다리·날개·꼬리는 두 본입니다. PSB에서는 분리된 부위의 문서 좌표를 사용하고, 프리팹에서는 타입별 동일한 이동량으로 부위를 조립합니다. Unity의 투명 영역 트리밍 때문에 개별 Sprite의 로컬 사각형과 본 좌표는 달라질 수 있습니다. 복사 기준은 PSB의 Character Skeleton입니다.

595개 부위마다 Unity 2D Animation의 알파 외곽 추출과 삼각화를 따로 실행했습니다. 생성된 삼각형을 한 번 세분화하고, 부위별 공통 관절축을 기준으로 정규화된 가중치를 적용했습니다. 외형끼리 메시를 복사하지 않습니다.

## 외형 교체와 애니메이션

각 프리팹에는 `CharacterRig`, `Animator`, 부위별 `SpriteSkin`이 있습니다. `CharacterAppearance` 에셋을 코드나 Inspector 참조로 받아 사용하세요.

```csharp
using DoodleIdle.CharacterRigs;

rig.SetAppearance(appearance); // 동일한 rigType만 허용
rig.SetMoving(true);           // Move / false이면 Idle
rig.Attack();
rig.Hit();
rig.Die();
```

각 Animator에는 Idle, Move, Attack, Hit, Death 클립이 있습니다. 파라미터는 Moving(bool), Attack/Hit/Die(trigger)입니다. Death는 마지막 자세를 유지합니다. 전투 판정, 이동 로직, 이펙트, 사망 후 제거는 게임 코드에서 연결해야 합니다.

무기 PNG 40개는 512×512 캔버스와 동일한 손잡이 기준점(이미지 좌표 96,416 / Unity pivot 96,96)을 사용합니다. `Weapon` 오브젝트는 팔2의 끝 본을 따라 움직입니다.

## 편집 및 재생성

Unity 메뉴 `Doodle Idle/Character Rigs/Build All`은 전체 PSB 본·메시와 프리팹을 재생성합니다. 수동으로 수정한 본·가중치·기본 애니메이션을 덮어쓰므로 커스텀 작업은 복제본에서 하세요. `Rebuild Prefabs Only`는 현재 Appearance를 사용하여 프리팹·기본 애니메이션만 다시 만듭니다. `Verify and Render`는 검증 보고서와 조립 미리보기를 만듭니다.

이 작업에서 기존 이미지 메타를 프로젝트 밖에 백업한 뒤 Unity로 다시 생성했습니다. 앞으로는 새 `.meta`를 유지하세요. PSB의 Reslice From Layer는 본과 메시를 보존하기 위해 꺼져 있습니다.

`Reports/build_report.json`은 생성 결과, `Reports/verification_report.json`은 스프라이트·외형 교체·애니메이션 변형 검사 결과입니다. `Previews`는 조립된 기본 자세와 공격 자세입니다.
