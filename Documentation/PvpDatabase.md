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
