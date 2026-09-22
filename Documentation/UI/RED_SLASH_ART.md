# 빨간검 2컷 교체

## 2026-09-23 작은 조각 제거 (현재 적용본)

- 내장 image_gen으로 기존 시트를 편집했다. 두 컷 안쪽의 분리된 작은 빨간 조각을 모두 제거하고 큰 검격만 남겼다.
- 저장: `Assets/DoodleIdle/Resources/DoodleIdle/SkillRedSlash.png` (1774×887 RGBA, 투명 배경, 887×887 두 칸). 기존 리소스 경로와 importer GUID, 공격 동작은 유지한다.
- 생성 원본: `C:/Users/user/.codex/generated_images/01a0c219-a594-7ff2-9400-6a0aa68d422f/exec-d278d0e3-103d-4d22-946d-80609a2376e8.png`.
- 검증: bccf6d8 / [GitHub Actions 35748767428](https://github.com/kuzuni/20260920/actions/runs/35748767428), 기존 검격 애니메이션·발사/잔상 및 전투 캡처 검사 2/2 통과 (22.035초). 실제 게임 캡처에서 큰 검격과 잔상에 분리된 빨간 조각이 없는 것을 확인했다. 로컬 Unity 실행 없음. 전체 검사는 재실행하지 않았다.
- 최종 편집 프롬프트:

> Use case: precise-object-edit. Edit target: attached transparent two-frame red sword-energy sprite atlas. Remove ONLY the detached small red scraps/streaks floating inside the open concave side of BOTH large crescents. Each cell must contain exactly ONE connected large crescent sword slash with no detached fragments or particles anywhere. Preserve the two existing main crescents exactly: their right-facing ')' orientation, size, position, different animation silhouettes, red/coral fill, thick hand-drawn black outline, and internal black decorative marks. Keep the connected forked lower tip of the second main crescent. Do not redesign, mirror, shrink, recenter, or add elements. Keep TWO equal-width side-by-side square animation cells, whole atlas 2:1, same framing and generous transparent margins. Genuine transparent alpha background; no opaque background, text, grid, shadow, or glow.

## 이전 생성 이력 (작은 조각 지시는 위 수정으로 대체)

- 도구: 내장 image_gen (CLI/API 미사용).
- 결과: `Assets/DoodleIdle/Resources/DoodleIdle/SkillRedSlash.png`, 2열×1행, 투명 배경. 런타임 공통 바운드/피벗으로 재생.
- 첫 스타일 참조: PlayerWalkB.png, 구형 RedSlashA.png. 최종 수정 참조: 첫 생성 시트.
- 최종 프롬프트:

> Revise this game sprite sheet: keep exactly TWO equal-width side-by-side frames, red/coral palette and thick black hand-drawn cartoon outlines, genuine transparent alpha background. Critical corrections: each energy slash must be shaped like a closing parenthesis ')' instead of the current opening parenthesis '('. The rounded continuous convex leading edge must point toward the RIGHT, the empty concave area opens toward LEFT. Both slash tips curl back to LEFT and all small trailing energy streaks are on LEFT side. Not a simple whole-image mirror because trailing streaks must remain behind to LEFT. This is a sword-energy projectile traveling RIGHT. Frame one: a broad thick half-moon ')' red slash with coral inner crescent. Frame two: noticeably different flowing energy phase, a narrower and taller sharp ')' crescent with elongated LEFT-pointing tips, split into two red ribbons at the trailing lower tip, and stretched leftward thin trailing red streaks. Same overall bounding size and center anchor between frames, no extra slash projectiles per cell. Make the TWO animation silhouettes clearly different at small size, not two almost identical crescents. No character, text, frame borders, shadow or glow background. Whole 2-frame atlas, not separated arm or object parts.
