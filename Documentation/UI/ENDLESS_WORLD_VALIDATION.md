# 소환 A/B와 경계 없는 맵 검증 — 2026-10-02

환경: 프로젝트 20260920, Windows Unity 6000.3.8f1 Editor, DirectX 12.

## 동작

- Player_Standard 프리팹의 SpawnZones/A_NoSpawn, B_SpawnBoundary는 활성 Trigger입니다. 반경 기본값은 4.5 / 16이며 Radius와 Offset을 편집합니다.
- 일반 적/보충/던전/보스는 몸통 전체가 A 밖, B 안에 들어가는 위치에서만 생성합니다. 공간 부족 시 생성 수를 억지로 채우지 않고 재시도합니다.
- 이동 A/B/C는 별도 유지합니다. 시각적 피격 확대는 소환 범위에 영향을 주지 않습니다.
- 맵 벽 및 플레이어/적의 사각 이동 제한을 기본 해제합니다. 한 장의 바닥을 카메라에 맞춰 재사용하고 월드 좌표 무늬를 유지합니다.
- 먼 좌표에서의 Y 정렬과 공간 검색 캐시 누적을 처리했습니다. 애니메이션 클립은 수정 대상에서 제외했습니다.

## 실행한 검사

- 관련 PlayMode 14개 통과: `artifacts/character-reports/endless-world-regression.xml`.
- 50/100마리, 원점·이동 위치·(1000, -2000)에서 소환 금지/최대 범위와 적 간격 검증.
- 오프셋·배율을 변경한 A, 막힌 범위, 3배 보스, B 확장 후 미완성 웨이브 보충 검증.
- 먼 좌표의 카메라/가로세로 화면에서 바닥 범위, 플레이어 이동과 캐릭터 깊이 정렬 확인.
- 적 집단을 150회 다른 먼 위치로 옮겨 공간 캐시가 제한되고 타겟이 유실되지 않음을 확인.
- 기존 정지/후퇴 A/B/C, Idle 공격, 등장 중 스킬 진행, 소환진 풀, 골드 정렬, PVP 경직 회귀 확인.
- 일반 전투 30초: 평균 16.69ms, 76회 처치. 에디터 표본이며 실기기 60fps 보장을 뜻하지 않습니다.
- 스크린샷 `artifacts/screenshots/endless-map-distant-world.png`: (-2000, -1000)에서 바닥/플레이어/적이 정상 표시되는 렌더를 확인했습니다.

최종 보강 검사 2개도 통과했습니다(`endless-world-final.xml`). 플레이어의 이동 방향에 적을 놓아 정지 범위에 걸렸던 테스트 배치를 옆 방향으로 옮겨, 기존 정지 기능과 장거리 이동 검사를 분리했습니다. 장거리 적 속도 유지와 B 확장 후 보충을 최종 코드로 다시 확인했습니다.

Windows Development 빌드(DoodleLogin + DoodleIdle): 2026-10-02 13:13, Succeeded / 0 errors. 출력: `Builds/Performance/DoodlePerformance.exe`.

전체 테스트 스위트를 실행한 결과는 아닙니다. 조절 방법은 MANUAL_BALANCE.md를 참조하세요.
