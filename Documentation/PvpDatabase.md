# PVP — BACKND Database

대상 프로젝트는 **20260920**. 기존 게임 정보의 Public 테이블 대신 BACKND Database를 사용한다.

## 콘솔 리소스

- Database UUID: `01a0f324-ba89-7171-b02a-9405d0382171` (Seoul, FREE)
- User table: `pvp_profile`
- READ: SELF + OTHERS / WRITE: SELF / client access enabled
- 자동 소유자: `user_uuid`; `account` string primary key not null; `score` int32 not null default 0; `summary` JSON; `payload` JSON
- `score` 인덱스: 승점 근처 상대 검색용. 기본 `user_uuid` 인덱스는 본인 조회용.
- 리더보드 `PvpRanking`: `01a0f351-e689-7c2c-a61d-7aaa62fd9b53`, `pvp_profile.score`, 내림차순, 초기화 없음.

식별 UUID는 비밀 키가 아니다. 인증 키가 포함된 실제 `Assets/DoodleIdle/Resources/DoodleIdle/BackendSettings.json`은 Git에서 제외된다. 설정 형식은 `BackendSettings.example.json` 참고.
공식 npm SDK `com.backnd.database` 0.0.18을 임베드해 사용한다. 실서버 재로그인/연결 종료 테스트에서 SDK Dispose가 다음 비동기 큐 프레임에 예외를 내는 문제를 확인해, 큐가 토큰을 미리 보관하고 Dispose가 중복 실행되지 않도록 수정했다. 변경 근거는 `Packages/com.backnd.database/DOODLE_PATCHES.md`에 기록했다. 기존 BACKND 기본 SDK는 유지한다. Android/Standalone/iPhone의 `BACKND_SDK_INSTALLED` 정의가 필요하다.

## 대전 및 저장

도전 → 승점에 가까운 최대 5명 → 선택 → 메인 필드 일시 정지 → 3, 2, 1 → 두 개의 동일 전투 엔진으로 자동 대전 → 결과 저장 → 필드 복원.
상대 선택 버튼에는 이름, 상대 승점, 전투력, 승리 가산점과 패배 감점을 함께 표시한다. 목록을 연 뒤 본인/상대 승점이 바뀌었다면 다시 조회하게 하여 표시된 보상과 정산 기준이 달라지지 않도록 한다.
서버에 PVP 프로필이 하나도 없을 때만 첫 경기 연습 상대를 제공한다. 통신 오류를 빈 서버로 취급하지 않는다. 등록 상대가 5명 미만이면 없는 자리는 대기 상태로 표시한다.

도전 시점의 스탯, 발견/강화/장착 장비·유물·스킬·동료, 획득/장착 스킨, 스킬 슬롯 해금 진행도, 공격 버프를 캡처한다. 상대는 마지막 경기에서 저장된 캡처를 사용한다. 지갑·일반 진행 보상은 상대 모델에 복사하지 않는다. 기존 스킬/동료 전투 코드와 리깅을 사용하며 애니메이션 파일은 수정하지 않는다.

승리 +1~5, 패배 -1~5. 점수 차이가 클수록 강한 상대 승리 보상이 커지고 패배 감점은 작다. 동점 상대는 ±3. 승점은 음수도 허용한다. 90초 회복 교착 시 남은 체력 비율로 판정하고 동률은 도전자 패배다. 하루 횟수는 기존 ServicesTuning을 따른다.

대전 시작 전에 고유 경기 ID와 캡처를 로컬 계정 저장소에 기록한다. 종료 시 DB 프로필/승점 → 리더보드 → 게임 정보 클라우드 저장을 순서대로 수행한다. 통신 실패 시 기록을 유지하고 추가 도전을 막으며 재시도 UI를 제공한다. 동일 경기 ID를 다시 전송해도 승점과 임무 보상은 중복 지급하지 않는다. 시작 후 앱을 종료해 완료되지 않은 경기는 복구 시 패배로 정산한다. 다른 기기에서 이전 기록이 바뀌면 덮어쓰지 않고 충돌로 처리한다.

후보 목록에는 요약만 조회하고 선택한 상대의 압축 전투 정보만 가져온다. 압축 데이터는 JSON 컬럼 한도에 여유를 두고 11,000 base64 문자, 압축 해제는 256 KB까지 제한한다. 랭킹은 상위 100명과 별도 본인 순위, 상위 3명 외형을 조회하고 30초간 재사용한다.

## 권한과 검증 범위

Database의 소유자 쓰기 권한은 타인의 기존 행 수정을 차단한다. 클라이언트에서 계산한 자기 전투 결과/승점의 진위까지 검증하는 서버 전투 판정은 아니다. 경쟁 서비스의 승점 조작 방지에는 서버에서 경기 검증/정산하는 별도 기능이 필요하다. 일 1만 명에 대한 부하 테스트나 요금제 용량 검증은 수행하지 않았으며 FREE 용량은 실제 읽기/쓰기량을 모니터링해야 한다.

테스트: `DoodleIdlePvpTests.cs` (전투/장착 복원/필드 복구), `DoodlePvpDatabaseTests.cs` (명시적 opt-in 실서버 소유권/조회/랭킹/중복 저장/클라우드 저장). 실서버 테스트는 `Library/PvpLive.optin` 존재 시만 실행하고 직접 만든 임시 계정만 사용한다.

공식 자료: [Database 모델](https://docs.backnd.com/sdk-docs/database/data-modeling/), [Database 리더보드 연동](https://docs.backnd.com/sdk-docs/database/samples/), [내 순위 조회](https://docs.backnd.com/sdk-docs/backend/base/leaderboard/user/get-mine/).

## 실서버 검증 (2026-10-01)

서로 다른 임시 계정 A/B로 실제 프로젝트에서 검증했다. A의 전투 캡처와 -3점 결과를 저장하고 같은 경기 ID를 재전송해도 점수가 -3점으로 유지됨을 확인했다. B는 A를 후보 목록에서 조회하고 전투 캡처를 읽을 수 있지만 A의 승점을 999999로 수정하는 요청은 서버의 소유권 검사로 거부됐다. B의 실제 PVP 화면에서 A를 선택해 전투하고, 승리 후 +1~5점, 랭킹 등록, 장착/스탯 캡처, 클라우드 게임 저장, 메인 HUD 복구까지 확인했다.

실서버 테스트 보고서: `artifacts/character-reports/pvp-database-live-tests.xml` (1건 통과). 테스트 계정은 모두 정리했고, 중간 실패에서 남았던 계정 한 개도 사용자 승인 후 콘솔에서 해당 UUID의 게임 데이터/랭킹과 함께 삭제했다. `Library/PvpLive.optin`은 검증 후 제거한다.

오프라인 전투/스킨·장착 복원/중단 복구/승패 양쪽 판정/기존 기본 공격·동료·스킬 회귀는 `pvp-combat-final-tests.xml`에서 4건 통과했다. ±1~5점 경계값 4건과 오프라인 PVP UI 검사는 `pvp-rules-and-ui-tests.xml`의 해당 통과 항목을 참고한다. 그 보고서의 이전 전투 지속시간 검사는 이후 `pvp-combat-final-tests.xml` 결과로 대체했다. 기존 애니메이션·컨트롤러·마스크 39개 해시는 모두 동일하다.

최종 준비 화면에서는 양쪽 리깅 초기화가 끝난 뒤 캐릭터를 표시하고 카운트다운을 시작한다. `pvp-countdown-final-tests.xml`에서 승리/패배·HUD 복구를 다시 통과했고 `artifacts/screenshots/pvp-two-player-field.png`로 양쪽의 몸/눈/입/무기 정렬과 색상을 확인했다. Windows Development Build는 2026-10-01 03:02 KST 기준 `Succeeded 0 errors`. Android 기기 실행 및 동시 접속 부하 검증은 별도다.


## 전체 장착 검증 (2026-10-01 추가)

`DoodlePvpLoadoutAuditTests.cs`에서 서로 다른 두 구성으로 장비 3부위, 유물, 스탯 20종, 치명타 단계 17개, 보유/장착 스킨, 공격 버프, 스킬 8칸과 동료 5칸을 검증한다. 구성당 477개 항목(파생 공격력/체력/회복/전투력, 아이템 상태, 개별 스킬·동료 피해/주기 등)을 직렬화 전후와 FixedUpdate 수치 캐시 활성화 후에 대조한다. 7개 편성으로 스킬 40종과 동료 32종 전체의 자동 발동/발사 및 실제 피해 발생을 양쪽에서 확인했다.

전체 기술의 명중을 관측하는 내구 시험에서는 수치 대조가 끝난 뒤에만 테스트용 actor HP를 늘려 조기 사망을 방지한다. 왕꿈틀이(GiantWorm)의 나선 궤도는 근접 이동 표적을 비껴갈 수 있어, 같은 자동 시전을 유지한 채 6유닛 거리의 정지 표적에서도 명중을 확인했다. 게임의 궤도·공격 주기·피해 공식을 바꿔 테스트를 맞추지 않았다. 세부 로그는 `artifacts/character-reports/pvp-full-loadout-audit.txt`이며, 해당 전체 기술 검사는 `pvp-loadout-path-audit.xml`에서 통과했다. 이 초기 보고서의 별도 피해 검사 실패(테스트의 public 메서드 반사 조회 오류)는 수정 후 `pvp-applied-damage-audit.xml`의 통과 결과로 대체한다.

추가 피해 검사는 기본/스킬/동료의 실제 최종 피해와 HP 차감, 스탯 증가, 장비 3부위 착용 효과, 유물·보유 스킨 효과, 스킬/동료 강화 효과 및 상대 데이터 격리를 검사한다. `PvpDamageDealt`는 피해를 낸 스킬/동료 ID와 최종 피해량을 관측하는 이벤트다. 구독자가 없으면 로그/목록을 만들지 않으며 일반 필드의 피해 공식은 동일하다.

실서버에서도 임시 A/B 계정에 서로 다른 스탯과 외형/무기 스킨, 스킬 8개, 동료 5개 및 전체 장비/유물 보유 효과를 적용했다. 다른 계정이 DB에서 불러온 상대 모델 및 도전자 모델의 각 477항목이 원본과 같고, 양쪽 각각 8개 스킬/5개 동료의 실제 피해가 기록됐다. 이 실전 시험은 HP를 별도로 늘리지 않고 저장된 실제 체력/회복을 사용했다. 보유 회복 효과가 큰 조합이므로 90초 체력 비율 판정까지 진행됐으며, 결과 -3점/랭킹/장착 스냅샷/클라우드 저장과 HUD 복구, 임시 PVP 행 삭제 및 두 계정 탈퇴 요청 성공을 확인했다. `pvp-database-loadout-live-tests.xml` 1건 통과 및 `pvp-live-loadout-audit.txt` 참고. 동작을 확인하려고 회복 밸런스를 낮추지는 않았다.

실서버 테스트의 Unity 오류 검사는 비동기 계정 정리가 끝날 때까지 수집한 뒤 단언한다. 오류 무시로 성공 처리하지 않는다. DOTween을 시험 시작 시 명시적으로 초기화하여 첫 테스트 씬 교체 도중 지연 생성되지 않게 한다. 실제 화면 캡처는 `artifacts/screenshots/pvp-loadout/`에 저장한다.

최종 재검증 `pvp-loadout-final-tests.xml`: 실제 FixedUpdate 회복량·피해 계산, 상대 목록 UI, 기존 기본 공격 토글/동료 스플래시, 전체 장착 실서버 대전의 5건 모두 통과. 실서버 실행 중 Error/Exception/Assert 로그도 0건이다. 애니메이션·컨트롤러·마스크 39개는 기존 해시와 동일하다.
`2026-10-01 03:45 KST` Windows Development Build도 `Succeeded 0 errors`. 최종 실행 후 실서버 opt-in 파일과 임시 에디터 실행기는 제거했다.


## 로그인 진입 경로 및 스킬 없는 외형 검증 (2026-10-01)

이전 PVP 시험의 별도 SDK 초기화가 로그인 화면의 재실행 오류를 가릴 수 있어 제거했다. 이제 실서버 PVP 시험도 `DoodleBackendSession.EditorLogin` 내부 초기화부터 수행한다. 수정/재현 결과는 `BackendIntegration.md`의 Editor authentication initialization 절 참고.

`PvpOpponentSkinsAndCompanionsRenderWithoutEquippedSkills`는 양쪽 장착 스킬을 0개로 저장하고 서로 다른 동료 5개씩, 수박/쿠키 외형과 무기 스킨을 적용한다. 상대 외형과 보유 효과는 DB에서 읽은 마지막 스냅샷으로 복원한다. 실전에서 스킬 자동 발동/피해가 0이고 양쪽 모든 동료가 발사·명중하는 것을 확인했다. 승점 저장 및 임시 계정 정리까지 `pvp-companions-live-tests.xml`에서 통과했다. 실제 캡처 `artifacts/screenshots/pvp-companions/03-geared-combat.png`에서 양쪽 외형·무기·동료를 확인할 수 있다. 런타임 피해 효과를 지우거나 합성한 이미지가 아니다.

`DoodlePvpOwnedEffectTests.cs`는 상대 모델에 대해 원시 카탈로그 수치를 사용하는 별도 double 산술과 실제 PVP 피해 및 FixedUpdate 캐시를 대조한다. 각 아이템 보유 효과 제거/강화, 장착 해제, 각 스킨 보유 효과 제거, 공격/체력/회복 스탯, 공격 버프, 치명타 단계별 50%·100%와 다음 단계 잠금을 검사한다. 보유 효과를 고립하여 검증하는 단계에서는 장착 상태를 고정한 채 discovered만 잠시 바꾸고 매 사례 후 원복한다. 체력/회복/골드 전용 효과가 공격 피해를 바꾸지 않는 것도 검증하며, 스킬/동료 고유 등급·강화 배율과 1타 피해·DPS도 대조한다. 로그: `artifacts/character-reports/pvp-opponent-owned-effects.txt`.

2026-10-01 추가 보유 효과 결과: `pvp-owned-effects-final-tests.xml` 통과. 498개 구성의 상대 수치·실제 기본/스킬/동료 피해가 독립 산술과 일치했다(비교 허용 상대 오차 0.002%). 일부만 보유한 혼합 구성도 477개 항목의 압축 저장/복원/캐시 대조를 통과했다. 치명타는 17개 각 단계에서 다음 단계에 저장된 레벨이 최대여도 선행 단계가 50%일 때 잠기는 것과, 선행 단계 100% 달성 후 실제 배율 적용을 확인했다. 이 범위에서는 보유 효과 누락이나 불필요한 공격 피해 가산을 발견하지 않았다. 모든 가능한 장비 조합의 완전 탐색이나 Android 실기기 시험을 의미하지는 않는다.
