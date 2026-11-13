# CG2 Starter Project v8
Unity 6000.5+ URP 2D / New Input System 대응

## 수정 이력
- v5: GraphicRaycaster 네임스페이스 수정 + StandaloneInputModule → InputSystemUIInputModule
- v6: Unity 6000.5.1f1 호환성 수정
     · linearVelocity 경고 대응 (PlayerController, EnemyAI, Projectile)
     · HitReceiver: MaterialPropertyBlock → material 인스턴스 방식 (SRP Batcher 경고 제거)
     · StarterSceneBuilder: TextAlignmentOptions.Top 명시, FindFirstObjectByType 유지
     · 코드 가독성 개선 (인덴트 정리)
- v7: 스프라이트 규격 버그 수정 + Unity 6 deprecated API 정리
     · [중요] StarterSceneBuilder.Save() 에 spritePixelsPerUnit = 64 명시
       미지정 시 Unity 기본값 100이 박혀, 스프라이트가 콜라이더보다
       1.4~1.7배 작아지는 히트박스 불일치가 있었다.
         Player 64px : PPU 100 → 0.64유닛  (콜라이더 지름 0.90)  ← 어긋남
                       PPU  64 → 1.00유닛  (콜라이더 지름 0.90)  ← 맞음
         Enemy  48px : PPU 100 → 0.48유닛  (콜라이더 지름 0.80)  ← 어긋남
                       PPU  64 → 0.75유닛  (콜라이더 지름 0.80)  ← 맞음
     · FindFirstObjectByType → FindAnyObjectByType
       Unity 6에서 deprecated. 인스턴스 ID 순서에 의존하는 API인데,
       해당 코드는 "EventSystem이 하나라도 있는가"만 보므로 순서가 무의미하다.

- v8: EnemySpawner 스폰 위치 검사 추가
     기존에는 플레이어 주변 반경 13유닛 원 위에 무조건 배치했다.
     맵에 벽이 생기면 두 가지 문제가 난다.
       · 플레이어가 가장자리에 있으면 원의 일부가 바깥 벽 너머로 나가 적이 갇힌다
       · 안쪽 기둥 위에 스폰되면 벽에 박힌 채 밀려나온다
     이제 후보 위치를 최대 12번 뽑아 "맵 안이고 벽이 아닌" 자리에만 배치한다.
     시도가 거듭될수록 반경을 줄여 플레이어 쪽으로 당기므로, 구석에 몰려도 자리를 찾는다.
     벽 판별은 태그나 레이어가 아니라 Rigidbody2D.bodyType == Static으로 한다
     (플레이어와 적은 Dynamic이므로 별도 설정 없이 지형만 걸러진다).
     타일맵이 없는 씬에서는 범위 제한이 걸리지 않아 v7과 동작이 같다.

## 임포트 후 씬 빌드
CG2 Starter → Step 1 → Step 2 → Step 3

## v6에서 올라오는 경우 (반드시 읽을 것)
v6로 이미 스프라이트를 만들었다면 그 PNG의 .meta 에는 PPU 100이 남아 있다.
v7을 임포트해도 기존 .meta 는 덮어써지지 않으므로, 둘 중 하나를 해야 한다.

  방법 A) Assets/StarterProject/Art 폴더를 지우고 Step 2 를 다시 실행
  방법 B) 4주차 패키지의
          CG2 Lab → 4주차 준비 → 1. 스프라이트 PPU 64로 통일   실행

캐릭터가 약 1.56배 커지지만, 콜라이더와 크기가 맞아떨어져 히트박스가 정확해진다.
3주차 sprites_assert(112장)는 이미 PPU 64 이므로 손댈 것이 없다.

## 조작
WASD / 방향키: 이동 (상하좌우 + 대각선)
자동 공격: 가장 가까운 적에게 투사체 발사

## 패키지 의존성 (Package Manager 필수 설치)
- Universal RP
- Input System
- TextMeshPro
