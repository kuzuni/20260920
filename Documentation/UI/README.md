# 게임 UI 기준과 사용 위치

최종 디자인 기준은 이 저장소의 [FinalDesign](FinalDesign)에 있는 이미지 25장입니다. 이후 수정에서도 이 사본을 참고하며, 바탕화면 원본 폴더에 의존하지 않습니다. 화면 전체 이미지를 런타임 배경으로 사용하지 않고 실제 UI 요소를 조합했습니다.

기존 시작 씬인 `Assets/Scenes/SampleScene.unity`와 `Assets/DoodleIdle/DoodleIdle.unity`에서 게임을 시작하면 UI가 자동으로 연결됩니다. 추가 Inspector 설정은 필요하지 않습니다.

- 구현 규칙과 원래 요청: [REQUEST.md](REQUEST.md)
- 데이터·전투 연결·외부 기능의 제한: [INTEGRATION.md](INTEGRATION.md)
- 검증 커밋·CI·캡처·남은 작업: [PROGRESS.md](PROGRESS.md)
- 그림체·생성 이미지·폰트 출처: [ART.md](ART.md)
- 시안 치수·화면별 배치 비교 기준: [REFERENCE_ALIGNMENT.md](REFERENCE_ALIGNMENT.md)

수치와 가격은 `Assets/DoodleIdle/Resources/DoodleIdle/UI/Collections.json`, `UI/Skins.json`, `UI/ServicesTuning.json`, `UiCommerce.json`에서 조정할 수 있습니다. 생성 아이콘은 같은 경로의 `UI/Icons.png`, `UI/GearIcons.png`, `UI/CurrencyIcons.png`에 있으며 기존 플레이어와 스킬 이미지도 함께 재사용합니다. 게임 수치는 공통 `UiNumber`로 1,000마다 a/b/c/d… 단위를 붙여 표시하며 저장값과 계산은 그대로 유지합니다.

전체 UI는 Canvas Scaler의 `Scale With Screen Size`, 기준 `720×1520(9:19)`, `Match Width Or Height`, `Match=1(Height)`을 사용합니다. HUD·팝업·아이콘·메뉴가 함께 같은 배율로 조절되며 기준 폭720의 배치는 중앙에 유지합니다. 화면이 짧아져도 가로로 늘어나거나 팝업만 별도로 축소되지 않습니다.

하단 네비의 메인 패널9종은 [최신 Inspector 지정](Specifications/BottomNavigationPanel.png)에 맞춰 크기 `679.9417×1232.817`, 중앙 앵커·피벗, 위치 `(0,33.8761)`, 안정 스케일1로 통일했습니다. 세부 팝업에는 이 메인 패널 규칙을 적용하지 않습니다.

이번 요청의 검증은 GitHub Actions 서버에서만 수행합니다. 로컬 Unity·플레이 모드·게임·테스트는 실행하지 않습니다. Actions의 `doodle-idle-test-results` 아티팩트에는 결과 XML, 로그, 실제 화면 PNG가 있습니다. 화면 비율은 720×1520, 720×1280, 900×900, 1440×900, 1520×720입니다.
