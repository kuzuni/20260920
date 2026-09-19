# 화면 없이 검증하기

`.github/workflows/doodle-idle-tests.yml`은 GitHub의 Ubuntu 서버에서 Unity 6000.3.8f1 PlayMode 테스트를 실행합니다. 개인 컴퓨터의 Unity 화면, 마우스, 키보드는 사용하지 않습니다.

## 최초 설정

GitHub 저장소 Settings → Secrets and variables → Actions에 아래 값을 등록합니다. 이번 작업에서 사용자가 Personal 라이선스용 시크릿 3개를 등록했습니다. 비밀번호나 라이선스 내용을 코드/채팅/커밋에 넣지 마세요.

- `UNITY_EMAIL`, `UNITY_PASSWORD`: Unity 계정 정보.
- Personal: `UNITY_LICENSE` (GameCI가 안내하는 활성화 라이선스 파일 내용).
- Pro: 대신 `UNITY_SERIAL`.

GameCI 공식 안내: https://game.ci/docs/github/activation/ 및 https://game.ci/docs/github/test-runner/

시크릿이 없으면 명확한 설정 오류로 종료합니다. 테스트를 통과한 것처럼 처리하지 않습니다. 시크릿 설정 후 브랜치 push 또는 Actions의 수동 실행으로 검사합니다. 테스트 XML과 Unity 로그는 Actions 아티팩트에서 내려받습니다.

## 자동 검증 항목

1. 생성 에셋 로딩, 초기 적 200마리, 바나나 5개.
2. 실제 Rigidbody2D 물리 시뮬레이션에서 겹친 적이 분리되는지.
3. 일시정지 중 시간/위치 정지, 재시작 시 200마리 복구.
4. 첫 대시가 5초 후 발동하는지.
5. 120초 자동 전투 중 평타/대시/바나나/돌멩이 타격 및 적 보충.
6. 이동 중 적 간 침투가 물리 솔버 허용 오차 이내인지.
7. 실제 URP 렌더링을 PNG로 저장하고, 바닥 위 캐릭터의 불투명한 몸통이 보이는지 픽셀 검사. `artifacts/screenshots`에 이전 셰이더 비교 화면, 수정 후 세로/가로 화면, 캐릭터 확대 화면을 저장합니다. 기능 테스트와 별도로 이미지를 직접 열어 확인합니다.
8. 화살 10발과 드론 미사일 20발의 정확한 개수, 서로 다른 발사 시각 및 최소 발사 간격.
9. 탱탱볼의 적 충돌 7회, 연속 동일 적 충돌 방지 및 7번째 충돌 후 오브젝트 소멸.
10. 불꽃의 서로 다른 표적 3명 및 잔상, 지렁이 몸통 순차 등장, 일시정지와 재시작 시 정리.
11. 새 스킬의 가로/세로 실제 렌더링(`05-new-skills-portrait`, `06-new-skills-landscape`)과 자동 전투 화면(`07-live-combat`).
12. 백그라운드 실행 플래그, 확대된 바나나 크기, 120초 전투 중 모든 새 스킬의 실제 명중 및 발사체 누적 방지.
13. 산탄 20발/발사 각도, 오이의 직선 이동과 여러 표적 관통, 대포 설치 위치와 정확한 10초 수명 및 범위 폭발.
14. 5방향 뱀의 순차 등장, 추적 뱀의 플레이어 연결/다단히트/사망 후 재추적, 검의 사거리 제한.
15. 탱탱볼/불꽃/모래/번개 잔상, 검정 사망 흔적의 투명도와 만료, 그림자 및 일시정지/초기화.
16. 드래곤 날개 2컷과 입에서 발사되는 불꽃, 빨간 검기 2컷과 실제 이동 속도.
17. `08-summons-landscape`, `09-summons-portrait`, `10-dragon-animation-next-frame`, `11-area-skills` 실제 서버 렌더링 캡처. 전체 자동 전투에서는 20개 스킬의 발동과 명중을 함께 확인합니다.

## CLI 방식도 가능

GitHub 서버가 아니라 화면 없는 로컬 검증을 원할 경우 Unity Test Runner의 `-batchmode -runTests -testPlatform PlayMode -testFilter DoodleIdle.Tests -testResults <xml 경로> -logFile <로그 경로>` 옵션을 사용합니다. 이미 열린 Unity 프로젝트와 충돌하지 않도록 별도 복사본에서 실행해야 합니다. `-runTests`와 `-quit`을 함께 전달하지 않습니다.

현재 문서는 테스트 실행 성공을 의미하지 않습니다. 실제 실행 결과를 별도로 확인해야 합니다.

## 파티클/전투 피드백 추가 검증

- 200마리 초기 개체수, 세로 확장 영역의 생성 및 물리 분리.
- 빨간 검기의 정확한 5연발/간격, 일반 검기와 같은 속도, 곡선 방향, 넓어진 범위, 2컷.
- 구름 2컷과 텍스처 바인딩, 보라색 추적 뱀의 플레이어 아래 정렬.
- 대포/먼지/모래/금화의 실제 ParticleSystem, 서버 GPU 렌더링 픽셀 비교, 일시정지/리셋.
- 실제 피해량에 따른 낙서풍 HP바 감소 및 손글씨 피해 숫자, 사망 지점의 둥근 검정 자국과 금화.
- 서버 캡처: 12-purple-tether-cloud, 13-particle-explosion-hp, 14-death-gold-coins.

- 대포 좌우 포구 발사점과 첫 포탄 위치, 실제 DOTween 반동/복귀/일시정지/리셋 검사 및 15-cannon-bounce-right, 16-cannon-bounce-left 캡처.
