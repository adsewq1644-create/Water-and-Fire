# Water and Fire 프로젝트 설정 가이드

## 1. 현재 테스트 씬

- `Assets/Scenes/PlantPuzzleTest.unity`
  - 기본 캐릭터 기능과 성장 식물 퍼즐을 함께 시험하는 메인 테스트 씬
- `Assets/Scenes/PrototypeCoreTest.unity`
  - 이동, 발사, 사망, 부활 등 1차 기능 확인용 씬

Build Settings의 0번 씬은 `PlantPuzzleTest`다.

## 2. 값을 수정하는 기본 원칙

### Inspector에서 수정할 값

캐릭터 물/불 표현, 불꽃 파티클, 식물 단계, 나무 연소 효과는 해당 오브젝트에 붙은 컴포넌트의 Inspector에서 수정한다.

- 물: `Water Sprite Wobble`
- 불 몸체: `Fire Sprite Flame`
- 불 주변 파티클: `Fire Sprite Edge Particles`
- 식물: `Growable Plant`
- 나무 연소: `Plant Edge Burn Effect`

이 컴포넌트들은 Inspector 값을 셰이더의 `MaterialPropertyBlock` 또는 런타임 머티리얼에 전달한다. 따라서 실행 중 생성된 Material이나 셰이더 프로퍼티를 직접 수정해도 다음 프레임에 컴포넌트 값으로 다시 덮어써질 수 있다.

### 스크립트나 셰이더 파일을 수정할 때

- Inspector 범위 안에서 외형만 조절: 컴포넌트 Inspector 사용
- 새로운 계산 방식, 왜곡 모양, 불꽃 알고리즘 추가: 셰이더 파일 수정
- 기본값 자체를 모든 새 오브젝트에 적용: 컴포넌트 스크립트의 기본 프리셋 수정
- 게임 규칙, 상태 전환, 입력 변경: C# 스크립트 수정

### Play 모드 주의

Play 모드에서 Inspector 값을 바꾸면 테스트에는 바로 반영되지만 Play 모드를 종료하면 보통 원래 값으로 돌아간다. 저장할 값은 Play 모드가 아닌 편집 상태에서 수정한다.

현재 두 테스트 씬에는 물/불 시각 컴포넌트가 미리 부착되어 있으므로 편집 상태에서 바로 설정할 수 있다.

`PlayerCharacter`는 안전장치로 속성에 맞는 시각 컴포넌트를 실행 시 자동 확인한다.

- Water이면 `WaterSpriteWobble` 유지
- Fire이면 `FireSpriteFlame`, `FireSpriteEdgeParticles` 유지
- 속성과 맞지 않는 반대쪽 시각 컴포넌트는 제거

시각 컴포넌트의 숨겨진 `presetVersion`이 현재 버전이면 Inspector 값은 시작할 때 기본값으로 다시 초기화되지 않는다. 단, 컴포넌트의 Reset을 실행하거나 향후 코드에서 프리셋 버전을 올리면 새 기본 프리셋이 적용될 수 있다.

## 3. 물 캐릭터

대상 오브젝트: `Player1_Water`

필수 컴포넌트:

- `SpriteRenderer`
- `Rigidbody2D`
- `Collider2D`
- `PlayerCharacter`
- `WaterSpriteWobble`

### Player Character

핵심 기능:

- 좌우 이동과 점프
- 마우스 드래그 투사체 발사
- 사망과 부활 상태
- 다른 플레이어와 물리 충돌 무시
- 5초 겹침 감지를 위한 파트너 연결
- 속성에 맞는 시각 효과 자동 확인

주요 설정:

- `Identity`: 플레이어 이름과 Water/Fire 속성
- `Input`: 이동, 점프, 상호작용, 발사 버튼
- `Movement`: 속도와 점프 감각
- `Combat`: 발사 쿨다운, 최소/최대 힘, 궤적 표시
- `Revive`: 부활 거리와 소요 시간

### Water Sprite Wobble

핵심 기능:

- 물 색상과 투명도
- 내부 물결 패턴과 반짝임
- 실루엣 흔들림
- 이동 속도에 따른 왜곡 변화
- 발광값 전달

값 수정 위치:

`Player1_Water > Water Sprite Wobble`

자주 조절할 값:

- `Deep Water Color`, `Soft Water Color`: 몸체 기본색
- `Inner Stream Color`: 내부 흐름 색
- `Rim Highlight Color`: 가장자리 색
- `Glow Color`, `Emission Intensity`: 발광 색과 세기
- `Transparency`: 투명도
- `Moving Wobble Strength`, `Moving Wobble Speed`: 이동 중 물결
- `Idle/Moving Sparkle Strength`: 반짝임
- `Idle/Moving Silhouette Wobble`: 외곽 흔들림
- `Idle/Moving Background Refraction`: 배경 굴절
- `Response`: 정지/이동 전환 반응 속도

사용 셰이더:

`Assets/Shaders/SpriteWaterWobble.shader`

일반적인 외형 조절은 셰이더 파일이 아니라 `WaterSpriteWobble` Inspector에서 한다.

## 4. 불 캐릭터

대상 오브젝트: `Player2_Fire`

필수 컴포넌트:

- `SpriteRenderer`
- `Rigidbody2D`
- `Collider2D`
- `PlayerCharacter`
- `FireSpriteFlame`
- `FireSpriteEdgeParticles`

### Fire Sprite Flame

핵심 기능:

- 캐릭터 몸체를 불 색으로 표현
- 내부 불꽃 패턴과 픽셀화
- 외곽 불꽃과 실루엣 흔들림
- 이동 반대 방향으로 불꽃 휨
- 발광값 전달

값 수정 위치:

`Player2_Fire > Fire Sprite Flame`

자주 조절할 값:

- `Core/Mid/Edge Color`: 불꽃 색 단계
- `Glow Color`, `Emission Intensity`: 발광
- `Body Alpha`: 몸체 투명도
- `Pixel Grid`: 픽셀 크기
- `Flame Tiling`: 불꽃 패턴 크기
- `Outer Flame Width/Strength/Wobble`: 외곽 불꽃
- `Silhouette Melt`: 실루엣이 녹듯 흔들리는 정도
- `Idle/Moving Flame Speed`: 불꽃 재생 속도
- `Idle/Moving Shape Shift`: 정지/이동 중 형태 변화
- `Wind Strength`, `Max Wind Bend`: 이동 반대 방향 휨
- `Response`: 움직임 반응 속도

사용 셰이더:

`Assets/Shaders/SpriteFireFlame.shader`

### Fire Sprite Edge Particles

핵심 기능:

- 몸체 위쪽과 좌우에 불꽃 파티클 생성
- 작은 불씨 생성
- 이동 방향에 따른 파티클 바람 반응

값 수정 위치:

`Player2_Fire > Fire Sprite Edge Particles`

자주 조절할 값:

- `Top/Side/Ember Emission Rate`: 생성량
- `Edge Band Thickness`, `Outside Offset`: 캐릭터와 파티클 거리
- `Min/Max Particle Size`: 불꽃 크기
- `Lifetime Range`: 유지 시간
- `Upward/Side Velocity Range`: 퍼지는 속도
- `Horizontal Wind Scale`: 이동 시 휘는 정도
- `Noise Strength/Frequency`: 불꽃의 불규칙함

실행 중 만들어지는 `FireEdgeParticles_*` 자식 파티클을 직접 수정하지 않는다. 부모의 `FireSpriteEdgeParticles` 값이 기준이다.

## 5. 성장 식물

기준 오브젝트: `GrowthPlant_Main_WaterGrow_FireShrink`

권장 구조:

```text
GrowablePlant Root
|- BoxCollider2D
|- GrowablePlant
|- PlantEdgeBurnEffect
|- PlantGrowthEdgeEffect
|- Visual
|  `- SpriteRenderer
`- ElementHitbox
   `- BoxCollider2D (Is Trigger)
```

### Growable Plant

핵심 기능:

- Sprout, SmallTree, LargeTree 단계 관리
- 물 투사체 피격 시 한 단계 성장
- 불 투사체 피격 시 한 단계 축소
- 단계별 스프라이트, 크기, 위치, 콜라이더 변경
- 단계 상태는 캐릭터 전체 사망 리셋과 분리되어 유지

값 수정 위치:

`식물 루트 > Growable Plant`

각 `Stages` 항목에서 설정:

- `Sprite`: 해당 단계 이미지
- `Color`: 단계 색상 보정
- `Visual Scale/Offset`: 이미지 크기와 기준 위치
- `Collision Role`: 통과/발판/벽 역할
- `Collider Size/Offset`: 실제 물리 충돌 영역
- `Hitbox Size/Offset`: 투사체 반응 영역

식물의 Transform이나 BoxCollider를 직접 조정해도 `GrowablePlant`가 단계 설정값으로 다시 적용할 수 있다. 단계별 모양과 충돌 크기는 반드시 `Stages` 안에서 수정한다.

식물 스프라이트는 다음 조건이 좋다.

- 투명 배경
- 단계마다 같은 Pixels Per Unit
- 단계마다 같은 Pivot, 권장값 Bottom Center
- 불꽃이 바깥으로 보일 투명 여백 포함

### Plant Edge Burn Effect

핵심 기능:

- 나무 실루엣 외곽에서 불꽃 생성
- 안쪽으로 타들어가는 경계 표시
- 아래로 흐르는 용융/드립 표현
- 나무 축소와 같은 시간 동안 재생

값 수정 위치:

`식물 루트 > Plant Edge Burn Effect`

자주 조절할 값:

- `Burn Duration`: 축소와 연소에 사용하는 시간
- `Hot/Flame/Char Color`: 열, 불꽃, 그을림 색
- `Edge Width`, `Front Width`: 타는 경계 두께
- `Outer Flame Width`: 실루엣 바깥 불꽃 폭
- `Noise Scale/Speed/Amount`: 불규칙한 연소
- `Melt Amount`: 안쪽으로 녹는 변화
- `Drip Length/Scale`: 아래로 흐르는 불길
- `Emission Intensity`: 발광

사용 셰이더:

`Assets/Shaders/SpritePlantEdgeBurn.shader`

실행 중 생성되는 `BurnEdgeOverlay`는 자동 오버레이이므로 직접 수정하지 않는다.

### Plant Growth Edge Effect

핵심 기능:

- 물을 맞아 성장할 때 현재 커지는 실루엣의 외곽만 초록색으로 반응
- 식물 내부를 채우지 않고 외곽 조각선과 바깥 발광만 생성
- 식물 크기 변화와 같은 시간 동안 재생
- Glow 레이어를 사용해 선택 블룸 적용

값 수정 위치:

`식물 루트 > Plant Growth Edge Effect`

자주 조절할 값:

- `Growth Duration`: 성장과 효과에 사용하는 시간
- `Core/Growth/Edge Color`: 중심광, 조각, 외곽 색
- `Fragment Scale`: 외곽 조각 무늬 개수와 크기
- `Fragment Edge Width`: 외곽 조각 선 두께
- `Rim Width`: 식물 외곽 효과 두께
- `Edge Noise Amount`: 외곽선의 불규칙함
- `Flow Speed`: 무늬가 흐르는 속도
- `Outer Glow Width`: 실루엣 바깥 발광 폭
- `Emission Intensity`: 발광 세기
- `Effect Opacity`: 성장 무늬 전체 투명도

사용 셰이더:

`Assets/Shaders/SpritePlantGrowthEdge.shader`

실행 중 생성되는 `GrowthEdgeOverlay`는 자동 오버레이이므로 직접 수정하지 않는다.

## 6. 투사체와 위험 요소

### Element Projectile

`PlayerCharacter`가 발사할 때 자동 생성한다.

- 같은 속성 캐릭터는 무시
- 반대 속성 캐릭터는 사망
- `GrowablePlant`에 닿으면 물은 성장, 불은 축소
- 일반 벽에 닿으면 제거

투사체는 캐릭터의 Glow 레이어를 물려받아 밝게 보인다.

### Prototype Hazard

Trigger Collider 오브젝트에 부착한다.

- `Common`: 물과 불 모두 사망
- `Water`: 물은 생존, 불은 사망
- `Fire`: 불은 생존, 물은 사망

## 7. 부활과 전체 사망

### Game Prototype Manager

씬에 하나만 둔다.

- Water Player와 Fire Player 참조 보유
- 두 플레이어의 파트너 연결
- 5초 겹침 사망 판정
- 둘 다 죽으면 시작 위치로 복귀

새 씬을 만들면 Manager의 Water/Fire 참조를 반드시 연결한다.

### Revive Gauge UI

- 죽은 캐릭터가 가진 `ReviveProgress` 표시
- 부활 진행도가 0보다 클 때만 표시
- 대상 캐릭터와 Fill Transform 참조 필요

## 8. 카메라

`Main Camera`의 `PrototypeCameraFollow`가 대상 캐릭터를 따라간다.

- `Target`: 따라갈 Transform
- `Offset`: 카메라 위치 차이
- `Smooth Time`: 추적 부드러움

## 9. Glow와 블룸

선택 블룸은 Unity Volume의 일반 Bloom과 별도 시스템이다.

관련 파일:

- `Assets/Scripts/Rendering/SelectiveGlowBloomFeature.cs`
- `Assets/Shaders/SelectiveGlowBloom.shader`
- `Assets/Editor/SelectiveGlowBloomInstaller.cs`
- `Assets/Settings/Renderer2D.asset`

규칙:

- Renderer가 있는 GameObject가 `Glow` 레이어면 선택 블룸 대상
- `Default` 레이어면 선택 블룸 제외
- 물/불 캐릭터는 실행 시 Glow 레이어로 설정
- 투사체는 의도적으로 Glow 사용
- 나무 연소 오버레이는 Glow 사용
- 포물선과 당김선은 Default 레이어와 `AimPreviewUnlit` 저휘도 셰이더를 사용해 블룸 제외

전체 블룸 세기 조절:

`Assets/Settings/Renderer2D.asset`을 선택하고 `Selective Glow Bloom` Renderer Feature의 값을 수정한다.

- `Threshold`: 밝은 부분을 블룸으로 인정하는 기준
- `Intensity`: 추출되는 빛의 세기
- `Iterations`: 블러 반복 수
- `Blur Radius`: 퍼지는 범위
- `Downsample`: 블룸 해상도와 부드러움
- `Composite Intensity`: 최종 합성 세기

`DefaultVolumeProfile`의 일반 Bloom은 Installer가 비활성화한다. 선택 블룸을 유지할 때는 Volume Bloom을 켜지 않는다.

## 10. 다른 오브젝트에 재사용하는 방법

### 다른 물 캐릭터

1. `SpriteRenderer`, `Rigidbody2D`, Collider 추가
2. `PlayerCharacter` 추가 후 Element를 Water로 설정
3. `WaterSpriteWobble` 추가
4. Manager의 Water 참조 또는 새 관리 구조에 연결

### 다른 불 캐릭터

1. `SpriteRenderer`, `Rigidbody2D`, Collider 추가
2. `PlayerCharacter` 추가 후 Element를 Fire로 설정
3. `FireSpriteFlame` 추가
4. `FireSpriteEdgeParticles` 추가
5. Manager에 연결

### 다른 성장 식물

가장 안전한 방법은 기존 `GrowthPlant_Main_WaterGrow_FireShrink`를 복제한 뒤 다음 값만 교체하는 것이다.

- 단계별 Sprite
- Visual Scale/Offset
- Collider Size/Offset
- Hitbox Size/Offset
- 시작 단계
- 연소 효과 설정

여러 씬에서 반복 사용하려면 최종 설정이 끝난 오브젝트를 Prefab으로 만드는 것이 좋다.

## 11. 문제 확인 순서

1. 오브젝트의 Element와 붙은 시각 컴포넌트가 일치하는지 확인
2. Play 모드가 아닌 상태에서 값을 수정했는지 확인
3. 블룸 문제면 Renderer가 있는 오브젝트의 Layer 확인
4. 식물 문제면 `Stages`의 Sprite, Scale, Collider 설정 확인
5. 실행 중 자동 생성된 자식 오브젝트를 직접 수정하지 않았는지 확인
6. Unity Console의 첫 번째 Error부터 확인
