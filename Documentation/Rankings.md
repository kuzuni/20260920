# 통합 랭킹 (20260920)

메인 출석 위의 랭킹 버튼으로 스테이지 / PVP / 전투력 탭을 연다. 각 탭은 상위 100명, 본인 순위, 새로고침을 제공한다. 행 왼쪽에는 해당 계정의 외형과 무기 스킨을 표시한다. 스테이지·전투력은 최근 업로드한 상태, PVP는 마지막 도전에서 저장한 상태를 사용한다. 아직 외형을 업로드하지 않은 이전 계정은 기본 플레이어로 표시한다.

## 서버 설정

- BACKND 프로젝트: **20260920**.
- 기존 StageRanking `01a0e88a-e607-7546-9c37-fab73d68fac5`: `StageProgress.stage`, 내림차순. 기존 기록을 유지한다.
- PvpRanking `01a0f351-e689-7c2c-a61d-7aaa62fd9b53`: `pvp_profile.score`, 내림차순.
- PowerRanking `01a0f4b0-e368-7b7d-877e-48e1ad56c8fe`: `StageProgress.powerScore`, 내림차순, 초기화 없음. 추가 필드는 `rankMeta`.
- Database `01a0f324-ba89-7171-b02a-9405d0382171`의 `rank_profile`: User table, `account` string primary key / not null, `metadata` string. 자동 소유자는 `user_uuid`. 읽기는 본인+타인, 쓰기는 본인만 허용한다.
- 로컬 BackendSettings의 `powerLeaderboardUuid`에 위 PowerRanking UUID를 설정한다. 예제 설정에도 반영되어 있다.

기존 StageRanking의 추가 필드는 생성 후 변경할 수 없으므로 별도 `rank_profile`에서 실제 조회된 상위 계정들의 외형을 한 번에 읽는다. PVP도 상위 계정들의 요약을 일괄 조회하며 전투 payload는 랭킹에서 읽지 않는다.

`rankMeta`와 `metadata`는 버전, 원래 전투력, 외형 키, 무기 키, 두 RGBA 색상을 담은 짧은 문자열이다. 전투력 정렬에는 log10 값을 사용해 double 범위를 넘는 게임 수치를 지원하고, 화면에는 원래 전투력을 표시한다. double 정밀도보다 가까운 log 값은 동점으로 취급될 수 있다.

스테이지·전투력은 해당 탭을 열거나 5분 주기 저장 시 업로드한다. 외형 변경은 스테이지 기록 갱신이 없어도 업로드된다. 팝업 결과 캐시는 계정별 30초이며 새로고침은 캐시를 건너뛴다.

화면에 보이는 행만 192px 정지 초상화를 만든다. 스크롤 밖의 리그·카메라는 해제하고 정지 초상화는 매 프레임 다시 렌더링하지 않는다. 원본 애니메이션 자산은 수정하지 않는다.

## 검증 (2026-10-01)

- `artifacts/character-reports/rankings-live-tests.xml`: 임시 계정 2개로 서버 업로드·세 탭 조회·상대 스킨/무기·원래 전투력 표기·메인 버튼 순서·탭 전환/닫기 검증 통과. 테스트 DB 행 삭제와 계정 탈퇴 요청 완료. Error/Exception/Assert 로그 0건.
- `rankings-metadata-tests.xml`: 4건 통과. 큰 전투력 정렬, 스킨·색상 왕복, 256바이트 제한, 이전 데이터 기본 외형.
- `rankings-scroll-tests.xml`: 100개 행에서 스크롤 전후 보이는 리그만 유지하고 팝업 닫힘 후 모두 해제되는지 확인, 1건 통과.
- 실제 화면: `artifacts/screenshots/rankings/rankings-main.png`, `rankings-tab-0.png` ~ `rankings-tab-2.png`.
- 기존 애니메이션·컨트롤러·마스크 39개 해시 동일. Android 기기 성능은 이번 변경에서 별도 측정하지 않았다.
- Windows Development Build: 2026-10-01 08:55 KST, Succeeded / 0 errors.
