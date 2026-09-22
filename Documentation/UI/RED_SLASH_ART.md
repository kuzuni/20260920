# 빨간검 2컷 교체

- 도구: 내장 image_gen (CLI/API 미사용).
- 결과: `Assets/DoodleIdle/Resources/DoodleIdle/SkillRedSlash.png`, 2열×1행, 투명 배경. 런타임 공통 바운드/피벗으로 재생.
- 첫 스타일 참조: PlayerWalkB.png, 구형 RedSlashA.png. 최종 수정 참조: 첫 생성 시트.
- 최종 프롬프트:

> Revise this game sprite sheet: keep exactly TWO equal-width side-by-side frames, red/coral palette and thick black hand-drawn cartoon outlines, genuine transparent alpha background. Critical corrections: each energy slash must be shaped like a closing parenthesis ')' instead of the current opening parenthesis '('. The rounded continuous convex leading edge must point toward the RIGHT, the empty concave area opens toward LEFT. Both slash tips curl back to LEFT and all small trailing energy streaks are on LEFT side. Not a simple whole-image mirror because trailing streaks must remain behind to LEFT. This is a sword-energy projectile traveling RIGHT. Frame one: a broad thick half-moon ')' red slash with coral inner crescent. Frame two: noticeably different flowing energy phase, a narrower and taller sharp ')' crescent with elongated LEFT-pointing tips, split into two red ribbons at the trailing lower tip, and stretched leftward thin trailing red streaks. Same overall bounding size and center anchor between frames, no extra slash projectiles per cell. Make the TWO animation silhouettes clearly different at small size, not two almost identical crescents. No character, text, frame borders, shadow or glow background. Whole 2-frame atlas, not separated arm or object parts.
