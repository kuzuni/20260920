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

1. 생성 에셋 로딩, 초기 적 80마리, 바나나 5개.
2. 실제 Rigidbody2D 물리 시뮬레이션에서 겹친 적이 분리되는지.
3. 일시정지 중 시간/위치 정지, 재시작 시 80마리 복구.
4. 첫 대시가 5초 후 발동하는지.
5. 120초 자동 전투 중 평타/대시/바나나/돌멩이 타격 및 적 보충.
6. 이동 중 적 간 침투가 물리 솔버 허용 오차 이내인지.

## CLI 방식도 가능

GitHub 서버가 아니라 화면 없는 로컬 검증을 원할 경우 Unity Test Runner의 `-batchmode -runTests -testPlatform PlayMode -testFilter DoodleIdle.Tests -testResults <xml 경로> -logFile <로그 경로>` 옵션을 사용합니다. 이미 열린 Unity 프로젝트와 충돌하지 않도록 별도 복사본에서 실행해야 합니다. `-runTests`와 `-quit`을 함께 전달하지 않습니다.

현재 문서는 테스트 실행 성공을 의미하지 않습니다. 실제 실행 결과를 별도로 확인해야 합니다.
