# 5주차 AI 에셋 패키지

`docs/asset_prompt.md` 프롬프트로 생성한 샘플 이미지 5장을 5주차 게임에 바로 적용하는 패키지입니다.

## 메뉴

| 메뉴 | 하는 일 |
|---|---|
| `CG2 Lab → 5주차 → AI 에셋 추가 — 샘플 11종 가공·교체` | `Raw/` 원본 5장을 아트 스튜디오와 같은 파이프라인으로 가공해 `Assets/CG2Week5/MyArt/`에 `ai_*.png` 11종으로 저장하고, Player · Enemy · Projectile · UI 아이콘 8종을 교체합니다. 처음 실행할 때 교체 전 상태를 `Backup/week5_before_ai.json`에 기록합니다. |
| `CG2 Lab → 5주차 → AI 에셋 → 기존 에셋으로 복구` | 기록해 둔 교체 전 스프라이트로 되돌립니다. `ai_*.png` 파일을 함께 지울지 고를 수 있습니다. 기록이 없으면 `되돌리기 — 4단계 에셋 교체`(Starter 플레이스홀더)를 대신 실행합니다. |

- 3단계 UI 구성 전이라 아이콘 세트가 없으면 플레이스홀더 세트를 먼저 만들고 그 위에 AI 아이콘을 넣습니다. 나중에 3단계를 실행해도 AI 아이콘은 유지됩니다.
- 5단계 Sprite Atlas가 이미 있으면 새 스프라이트로 다시 묶습니다.
- 메뉴 실행 후 씬을 저장(Ctrl+S)해야 Player 교체가 유지됩니다.
- 필요 조건: 5주차 실습 코드(`Assets/CG2Week5`)가 있는 프로젝트.

## 가공 규격

| 저장 이름 | 원본 | 크기 | 결과 내용물 |
|---|---|---|---|
| `ai_player_knight` | `ai_player_knight.png` | 64px | 43×62 |
| `ai_enemy_slime` | `ai_enemy_slime.png` | 48px | 46×41 |
| `ai_projectile_orb` | `ai_projectile_orb.png` | 24px | 22×22 |
| `ai_icon_hp` `exp` `coin` `damage` | `ai_icon_sheet_a.png` 칸 0~3 | 32px | 30×26 ~ 30×30 |
| `ai_icon_firerate` `range` `heal` `speed` | `ai_icon_sheet_b.png` 칸 0~3 | 32px | 20×30 ~ 30×30 |

공통 설정: 배경 허용 오차 0.12 · 전역 제거(크로마키) 켬 · 1px 외곽선 #14121A.

## 원본에서 미리 정리한 것

`docs/SAMPLE`의 생성 원본과 `Raw/`의 차이입니다.

1. **워터마크 제거**: 다섯 장 모두 오른쪽 아래(880~927px)에 생성 도구의 ✦ 표시가 있어 마젠타로 덮었습니다.
2. **플레이어 바닥띠 제거**: `player.png` 아래 130px(894~1023행)이 어두운 남색 바닥이었습니다. 그대로 두면 이 색이 배경 대표색으로 뽑히고, 캐릭터의 검은 외곽선까지 함께 지워집니다(64px 결과의 불투명 픽셀 1,812 → 1,361로 약 25% 소실). 프롬프트 규칙 1의 "no floor"를 어긴 경우입니다.

## 에셋 출처

| 에셋 | 생성 도구 | 생성일 | 프롬프트 | 원본 파일 |
|---|---|---|---|---|
| player_knight | Gemini (✦ 워터마크) | 2026-10-01 | asset_prompt.md §2 | `docs/SAMPLE/player.png` |
| enemy_slime | Gemini | 2026-10-01 | §3 | `docs/SAMPLE/enemy.png` |
| projectile_orb | Gemini | 2026-10-01 | §4 | `docs/SAMPLE/Projectile.png` |
| 아이콘 시트 A | Gemini | 2026-10-01 | §5 | `docs/SAMPLE/아이콘 시트 A.png` |
| 아이콘 시트 B | Gemini | 2026-10-01 | §6 | `docs/SAMPLE/아이콘 시트 B.png` |

이용약관 링크는 생성한 계정의 서비스 약관을 확인해 채워 넣으세요.
