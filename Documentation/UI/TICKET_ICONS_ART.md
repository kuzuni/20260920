# 미션 뽑기권과 마일리지 쿠폰

- 내장 image_gen 사용, 3열×2행 아틀라스. 경로: `Assets/DoodleIdle/Resources/DoodleIdle/UI/TicketIcons.png`.
- 순서: 갑옷, 몽둥이, 스킬 / 동료, 유물, 사용하지 않는 이전 마일리지 시안. 실제 알파 채널에서 셀 사이 배경은 0, 티켓 내부는 254이며 RGB에 남은 배경색은 표시되지 않는다.
- 최종 마일리지는 별도 `MileageCoupon.png`의 파란 다이아 카드로 교체했다. 최종 프롬프트는 `MILEAGE_DIAMOND_CARD.md`에 있다. 아래 프롬프트의 금색 별 티켓은 더 이상 게임에 표시하지 않는다.
- 생성 프롬프트:

> Create a transparent sprite atlas for a cute hand-drawn idle game, 3 columns by 2 rows of SIX distinct paper ticket icons, evenly spaced in six equal square cells. Use the supplied ticket only as the STYLE reference: thick wobbly black ink outline, cream paper, simple pastel colored border, semicircular ticket notches, small dashed perforation. Each ticket tilted gently upward right and centered in its cell with transparent margins. No words, letters or numbers anywhere. Top row left: light blue bordered ARMOR summon ticket with a simple gray breastplate emblem. Top row middle: tan bordered CLUB summon ticket with a small wooden spiked club emblem. Top row right: coral bordered SKILL summon ticket with a red meteor and flame emblem. Bottom row left: green bordered COMPANION summon ticket with a tiny cute green tank companion emblem, no player/cat. Bottom row middle: lavender bordered RELIC summon ticket with a cream round blue-pattern pottery jar emblem. Bottom row right: GOLD MILEAGE coupon with a gold border and a large blue diamond emblem. Very simple emblem silhouettes suitable for 32 pixel UI, no tiny textures, no hatching, no complex patterns. All six tickets are complete separate objects; do not join their edges. Genuine transparent alpha background throughout; no grid lines, labels, shadows, floating marks or extra symbols. 3:2 whole atlas.

- 최종 투명 배경 정리 프롬프트:

## 사용자 후속 수정

동료 탱크 문양을 날개 달린 구름 동료로, 마일리지의 다이아 문양을 별 도장으로 교체했다. 마일리지는 뽑기권이 아니라 5/10개 단위 다이아 교환 쿠폰이다.

> Edit exactly TWO emblems in this 3 columns x 2 rows transparent ticket atlas. Keep all six tickets in the SAME positions and same sizes, same outlines and border colors. Bottom-left GREEN ticket: REMOVE the tank and replace with a cute round lavender CLOUD COMPANION with two little feathered wings, tiny consistent black dot eyes and smile, a flying friend character. It must clearly be a companion character, not a vehicle and not the player's cat. Bottom-right GOLD ticket: REMOVE the blue diamond completely and replace it with a simple golden circular loyalty stamp containing a black five-point star, like a mileage coupon seal. NO diamond or gems on this gold coupon. Leave the other FOUR tickets exactly as they are (armor, club, meteor skill, pottery relic). Keep transparent alpha background everywhere outside the paper tickets including punched notches. No glow, no cast shadows, no backdrop, no text or numbers. Preserve simple hand-drawn thick black ink style, 1536x1024 atlas, 3 columns and 2 rows.

> Precise background removal only. Use attached 3x2 six-ticket atlas as edit target. Erase ALL the dark background and ALL colored outer glow halos to genuine transparent alpha, including all spaces between the six tickets, all corners and the inward punched ticket notches. Keep ONLY the six paper ticket silhouettes with their black border outlines and their internal colored emblems. Preserve every ticket's existing position, size, ink outline and colors exactly. No redesign. No new shadows, no glow, no backdrop, no checkerboard drawing. Exactly six isolated complete tickets on actual transparent alpha. Keep same 1536x1024 atlas layout and 3 columns, 2 rows.
