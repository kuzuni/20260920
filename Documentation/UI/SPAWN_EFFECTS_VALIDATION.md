# 소환/피격 효과 검증 — 2026-10-02

환경: Windows Unity 6000.3.8f1 Editor, DirectX 12, 프로젝트 20260920.

## 실제 확인한 범위

- 선택한 테스트 31개: 각 테스트의 최종 실행 결과 31개 통과, 0개 실패. 전체 프로젝트 테스트 스위트 실행을 뜻하지 않습니다.
- 회전하는 원형 자식 + Y 0.35 부모 소환진, 등장 전 공격/충돌 제외, DOTween 확대, 풀 재사용, 150번 겹쳐 요청해도 소환진 100개 제한.
- 50/100마리 소환: 중앙/이동 위치/맵 모서리에서 금지 영역과 적 최소 간격 유지.
- 등장 대기 중 기존 스킬의 이동 지속, 대기 중 타겟 없는 신규 스킬의 안전한 거절, 일시정지 후 등장 전 물리 활성화 방지.
- 피격 반복 시 같은 트윈 재사용과 managed 할당 상한, 스케일/흰색/시간 설정, 사망/풀 반환/PVP 경직.
- 골드 크기/착지 높이/개별 Y 정렬/실제 픽셀 투명도, 데미지 숫자 정지·축소·상승/페이드/128개 풀, HP바 캐시.
- 공통 HitBlood 설정 전파 EditMode 2개 통과. Burst가 없고 Rate over Time만 설정한 프리팹의 방출도 확인.
- 스킬 40종 각각 2회 이상 발동, 동료 32종 전원 실제 공격 확인. 이 전수 검사는 Time.timeScale=4인 가속 검사입니다.
- 최종 시작 시 DOTween 용량 예약 후 자동 용량 확장 경고 없음.

## 측정 결과와 범위

- 일반 전투 30초: 평균 16.68ms, 실제 처치 78회, 보충 1회.
- 스킬 8개 + 동료 5개 부하 표본 300프레임: 평균 16.76ms, p95 21.27ms.
- 스킬/동료 단독 검사 구간의 가장 높은 평균 프레임 시간: 16.86ms.
- 에디터와 렌더 대기를 포함한 측정입니다. 모바일 실기기 측정 또는 모든 프레임 60fps 보장을 뜻하지 않습니다.

## 로컬 증거

- `artifacts/character-reports/spawn-effects-validation-summary.json`: 테스트별 최신 통과 결과 및 원본 XML 매핑.
- `artifacts/character-reports/spawn-effects-*.xml`: 회귀/성능/시작/프리팹 전파 실행 원본. 회귀 파일에 남은 보스 방향 fixture 실패는 후속 성능 실행에서 수정 후 통과했습니다.
- `artifacts/performance/all-skills-companions.csv`: 스킬 40/동료 32 결과.
- `artifacts/performance/normal-main.json`, `spawn-effects-optimized.json`, `spawn-effects-optimized-markers.txt`.
- `artifacts/screenshots/summon-circle-before-enemy.png`, `summon-circle-growing-enemy.png`.

조절 위치와 이미지 생성 프롬프트는 `MANUAL_BALANCE.md` 및 `SPAWN_CIRCLE_PROMPT.md`를 참조합니다.

Windows Development 빌드(DoodleLogin + DoodleIdle): 2026-10-02 03:35, Succeeded / 0 errors. 출력: Builds/Performance/DoodlePerformance.exe.
