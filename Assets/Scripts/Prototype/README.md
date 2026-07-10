# Prototype Systems Guide

이 폴더는 Water-and-Fire 1차 프로토타입용 런타임 스크립트를 기능별로 정리한 곳입니다.

## Folder Map

- `Characters`
  - 플레이어, 투사체, 캐릭터 비주얼, 테스트용 속성 전환.
- `Core`
  - 공용 enum/interface, 게임 매니저, 카메라, 기본 위험 판정.
- `Plants`
  - 물/불 투사체로 성장/퇴화하는 식물 플랫폼.
- `Platforms`
  - 반동 발판, 잎사귀 발판, 움직이는 발판, 다리, 저울, 증기꽃, 물먹는 버섯, 금간 발판.
- `Hazards`
  - 떨어지는 불씨와 스포너.
- `WaterFlow`
  - Blob 기반 물 흐름, 물 위험 판정.
- `Darkness`
  - DarkZone, 불빛/물빛, 불빛 Reveal, 실루엣.
- `Rescue`
  - 추락 구조 매니저와 스킬 체크 UI.

과거 Raycast 선 기반 WaterSplit/Redirect/Blocker 구조는 더 이상 사용하지 않습니다.
현재 물은 `WaterFlowSource`가 생성하는 `WaterFlowBlob` 중심으로 동작합니다.

## Core Scene Setup

### GamePrototypeManager

씬에 하나만 둡니다.

- `Water Player`
  - 물 캐릭터 `PlayerCharacter`.
- `Fire Player`
  - 불 캐릭터 `PlayerCharacter`.
- `Overlap Death Seconds`
  - 물/불 플레이어가 서로 겹친 채 유지되면 둘 다 죽는 시간.
- `Projectile Sprite`
  - 플레이어 투사체 기본 스프라이트. 비우면 런타임 원형 스프라이트 생성.
- `Box Sprite`
  - 테스트용 박스 스프라이트. 비우면 런타임 사각형 스프라이트 생성.

### PrototypeCameraFollow

카메라에 붙입니다.

- `Target`
  - 따라갈 대상. 구조 이벤트 중에는 `FallRescueManager`가 임시로 바꿀 수 있습니다.
- `Offset`
  - 카메라 위치 오프셋. 더 멀리 보려면 일반적으로 Z와 Orthographic Size를 같이 봅니다.
- `Smooth Time`
  - 따라가는 부드러움. 낮을수록 빠르게 따라감.
- `Orthographic Size`
  - 2D 카메라 시야 크기. 캐릭터를 더 멀리서 보려면 이 값을 키우는 게 핵심입니다.

## PlayerCharacter

플레이어 본체에 붙입니다.

### Identity

- `Player Id`
  - 디버그/구조 UI 표시용 이름.
- `Element`
  - `Water`, `Fire`, `Common`.

### Input

- `Move Left Key`, `Move Right Key`
  - 좌우 이동.
- `Jump Key`
  - 현재 점프/차지/내려찍기 입력. 기본 `Space`.
- `Charge Cancel Key`
  - 차지 중 취소. 기본 `S`.
- `Interact Key`
  - 상호작용.
- `Fire Mouse Button`
  - 투사체 발사 마우스 버튼.

### Fall Rescue

- `Enable Fall Rescue Input`
  - 이 캐릭터가 직접 구조 요청 키를 받을지.
- `Rescue Request Key`
  - 이 캐릭터가 구조 요청에 쓰는 키. 로컬 테스트에서 두 캐릭터를 동시에 켜면 서로 다른 키로 두는 것이 안전합니다.
- `Stable Ground Tag`
  - 마지막 안전 위치 저장에 사용할 안정땅 태그입니다. 기본값은 `SafeGround`입니다.

멀티 구현 시에는 로컬 입력을 받은 캐릭터가 `FallRescueManager.RequestRescue(this)`를 호출하면 됩니다.

안정땅 판정:

- 플레이어가 `Ground Mask`에 포함된 Collider 위에 서 있어야 합니다.
- 그 Collider 또는 부모 오브젝트 중 하나가 `Stable Ground Tag`와 같은 태그를 가지고 있어야 합니다.
- 기본 `Assets/Prefabs/Ground/Ground.prefab`에는 `SafeGround` 태그가 붙어 있습니다.
- 구조 저장 지점으로 쓰면 안 되는 발판은 `SafeGround` 태그를 빼면 됩니다.
- 벽으로만 사용할 때는 `Assets/Prefabs/Ground/Wall.prefab`을 사용합니다. 이 프리팹은 `Untagged`이며 `SpriteRenderer + BoxCollider2D`만 포함합니다.

### Movement

- `Move Speed`
  - 지상 이동 속도.
- `Air Control Multiplier`
  - 일반 공중 좌우 조작 비율.
- `Ground Acceleration`, `Ground Deceleration`
  - 지상 가속/감속.
- `Air Acceleration`
  - 공중에서 목표 속도로 보정되는 속도.
- `Coyote Time`
  - 발판에서 살짝 떨어진 뒤에도 점프 허용 시간.
- `Ground Mask`
  - 땅으로 볼 레이어.
- `Ground Cast Distance`
  - 아래쪽 접지 검사 거리. 씬 Gizmo에서 확인 가능.
- `Side Cast Distance`
  - 옆 벽 검사 거리.
- `Minimum Ground Normal Y`
  - 이 값보다 위쪽을 향한 표면만 땅으로 인정.

### Charge Jump

- `Max Charge Time`
  - 풀차지까지 걸리는 시간.
- `Min Jump Power`, `Max Jump Power`
  - 최소/최대 차지 점프 세기.
- `Horizontal Jump Power`
  - 점프 시 좌우 방향 속도.
- `Min Horizontal Charge Multiplier`
  - 낮은 차지에서도 최소로 보장되는 좌우 힘.
- `Charge Move Speed Multiplier`
  - 차지 중 이동 속도 비율. 0이면 차지 중 제자리 고정.

### Jump Feel

- `Jump Motion Speed Multiplier`
  - 점프 궤적 모양은 유지하면서 전체 점프 시간을 압축하는 값. 1.0은 원래 속도, 1.2~1.3은 더 빠른 점프감.
- `Preserve Jump Arc While Reducing Air Time`
  - 켜면 초기 속도와 중력 보정을 같이 적용해 높이/거리 느낌을 최대한 유지.

### Full Charge Feel

- `Full Charge Threshold`
  - 이 비율 이상이면 풀차지로 판단.
- `Full Charge Gravity Multiplier`
  - 풀차지 점프 중 중력 느낌. 낮을수록 둥실함.
- `Full Charge Air Slow Multiplier`
  - 풀차지 점프 중 공중 좌우 보정 속도. 1이면 감속 없음.

### Dive

- `Dive Speed`
  - 공중에서 점프키를 다시 눌렀을 때 내려찍기 속도.
- `Dive Landing Stun`
  - 일반 바닥에 내려찍기 착지 후 경직.
- `Shockwave Radius`
  - 내려찍기 착지 충격파 범위.
- `Shockwave Duration`
  - 충격파 표시/판정 시간.
- `Shockwave Delay By Distance`
  - 거리별로 충격파 반응을 지연할지.
- `Shockwave Mask`
  - 충격파가 닿을 레이어.

### Down Slam Bounce Lock

- `Use Apex Percent Down Slam Lock`
  - 반동 발판으로 튕긴 직후 다시 내려찍기 잠금을 최고점 예상 시간 비율로 계산.
- `Unlock At Apex Time Percent`
  - 최고점까지 걸릴 예상 시간 중 몇 %가 지나면 내려찍기 재사용 허용.
- `Min/Max Down Slam Bounce Lock Time`
  - 잠금 시간 최소/최대.
- `Debug Down Slam Bounce Lock`
  - 재사용 가능 타이밍 디버그 표시.

### Combat

- `Projectile Cooldown`
  - 투사체 재발사 대기 시간.
- `Projectile Min/Max Force`
  - 끌어서 발사할 때 최소/최대 힘.
- `Projectile Lifetime`
  - 투사체 유지 시간.
- `Projectile Spawn Offset`
  - 캐릭터 중심에서 투사체가 생성되는 거리.
- `Projectile Visual Scale`
  - 보이는 투사체 크기.
- `Projectile Collider Radius`
  - 투사체 실제 충돌 반경.
- `Projectile Speed Multiplier`
  - 궤적은 유지하고 실제 속도만 빠르게 하는 배율.
- `Projectile Max Upward Velocity`
  - 고각 발사 시 위쪽 속도 제한. 0이면 끔.
- `Trajectory Points`, `Trajectory Step`
  - 궤적 미리보기 점 개수/간격.

## Plants

Prefab:

- `Assets/Prefabs/Plants/SeedType1_WideGrowPlant.prefab`
- `Assets/Prefabs/Plants/SeedType2_VerticalGrowPlant.prefab`

### GrowablePlant

물 투사체를 맞으면 한 단계 성장, 불 투사체를 맞으면 한 단계 퇴화합니다.
씨앗 단계에서 불을 맞으면 무시됩니다.

- `Starting Stage`
  - 처음 배치할 단계. 같은 씨앗을 3단계부터 시작하고 싶으면 여기서 조절.
- `Stages`
  - 단계별 비주얼/콜라이더/히트박스 설정.
- `Display Name`
  - 인스펙터 표시용.
- `Sprite`, `Color`
  - 단계별 스프라이트/색.
- `Visual Scale`, `Visual Offset`
  - 보이는 크기/위치.
- `Collision Role`
  - 플랫폼/벽/장식 역할.
- `Collider Size`, `Collider Offset`
  - 실제 플레이어가 밟거나 막히는 콜라이더.
- `Hitbox Size`, `Hitbox Offset`
  - 투사체를 받는 감지 영역.

## Platforms

### MovingPlatform2D

Prefab: `Assets/Prefabs/Platforms/MovingPlatform_Right.prefab`

오른쪽으로 계속 가려고 하다가 앞이 막히면 멈추고, 막힘이 사라지면 다시 이동합니다.

- `Move Direction`
  - 이동 방향.
- `Move Speed`
  - 이동 속도.
- `Limit Mode`
  - `Unlimited`: 계속 이동 시도.
  - `Local Distance`: 시작 위치 기준 거리 제한.
  - `World Target`: 목표 월드 좌표까지 이동.
- `Blocking Mask`
  - 무엇에 막힐지.
- `Ignore Player Characters`
  - 플레이어가 앞에 있어도 플랫폼 진행을 막지 않음.
- `Skin Width`
  - 벽에 닿기 전 남기는 안전 거리.
- `Min Blocking Normal Dot`
  - 얼마나 정면으로 막는 표면을 벽으로 볼지.
- `Rider Carry`
  - 위에 탄 플레이어를 플랫폼 이동량만큼 같이 이동.
- `Rider Probe Height`, `Rider Probe Extra Width`
  - 탑승자 감지 범위.

### SlipperySurface2D

Prefabs:

- `Assets/Prefabs/Ground/SlipperySlope.prefab`

중력 방향을 경사면에 투영해서 캐릭터를 아래쪽으로 미끄러지게 하는 경사면 전용 컴포넌트입니다.

배치 규칙:

- Collider는 플레이어의 `Ground Mask`에 포함된 레이어에 있어야 합니다.
- 구조 저장 지점이면 안 되므로 `SafeGround` 태그는 붙이지 않습니다.
- `SlipperySurface2D`가 붙은 표면은 실수로 `SafeGround` 태그가 붙어도 마지막 안전 위치로 저장되지 않습니다.
- 자식 Collider를 쓰는 경우 부모 오브젝트에 `SlipperySurface2D`를 붙여도 인식됩니다.

- `Prevent Safe Ground`
  - 이 표면을 구조 시스템의 안정땅 저장 대상에서 제외.
- `Enable Ground Slide`
  - 플레이어가 이 표면을 밟았을 때 경사 아래 방향으로 미끄러짐.
- `Ground Slide Acceleration`
  - 지상 미끄럼 속도에 도달하는 가속도.
- `Max Ground Slide Speed`
  - 지상/경사면 최대 미끄럼 속도.
- `Ground Input Control`
  - 미끄러지는 중 좌우 입력을 얼마나 허용할지. `0`이면 거의 못 버티고, `1`이면 입력 영향이 큼.
- `Exit Carry Time`
  - 미끄럼 표면에서 벗어난 직후 미끄러지던 X속도를 보존하는 시간. 경사 끝에서 수직으로 뚝 떨어지는 느낌을 막습니다.

동작 메모:

- 경사면 위에서는 표면 normal에 중력을 투영해서 미끄러지는 방향을 계산합니다.
- 미끄럼 표면에서 벗어나는 순간에는 마지막 미끄럼 속도를 `Exit Carry Time` 동안 짧게 보존합니다.
- 이 보존 때문에 `SlipperySlope` 끝에서 캐릭터가 수직으로 뚝 떨어지지 않고, 미끄러지던 방향으로 계속 날아갑니다.
- 일반 벽에는 `SlipperySurface2D`가 필요하지 않습니다. `SafeGround` 태그를 붙이지 않은 일반 Collider로 배치하면 구조 저장 지점이 되지 않으며, 중력에 따라 자연스럽게 내려옵니다.
- 벽 접촉 후 착지까지 공중 조작을 잠그거나 벽 방향 X속도를 강제로 0으로 만드는 별도 로직은 사용하지 않습니다.

### BouncePlatform2D

Prefab: `Assets/Prefabs/Platforms/BouncePlatform.prefab`

직접 내려찍기만 반응합니다. 일반 착지와 Shockwave는 반응하지 않습니다.

- `Bounce Vertical Velocity`
  - 위로 튕기는 속도.
- `Bounce Horizontal Velocity`
  - 반동 순간 좌/우 입력이 있을 때 대각선 반동 X속도.
- `Release Delay`
  - 눌린 뒤 튕겨내기까지 딜레이.
- `Cooldown`
  - 연속 반응 방지 시간.
- `Compress Depth/Time`, `Recover Time`
  - 비주얼 눌림 깊이/시간/복귀 시간.

### LeafBouncePlatform2D

Prefab: `Assets/Prefabs/Platforms/LeafBouncePlatform.prefab`

일반 발판처럼 설 수 있지만, 직접 내려찍기하면 발판 각도와 찍은 위치, 내려찍기 높이에 따라 반동합니다.

- `Min/Max Effective Dive Height`
  - 내려찍기 높이로 반동 세기를 계산하는 범위.
- `Min/Max Bounce Speed`
  - 낮은/높은 내려찍기 때 반동 속도.
- `Max Angle Error`
  - 중심에서 벗어날수록 최대 몇 도 틀어질지.
- `Edge Speed Multiplier`
  - 가장자리에서 힘이 얼마나 줄어들지.
- `Sweet Spot Width`
  - 중앙 판정 폭. Gizmo로 확인 가능.
- `Compress Depth/Time`, `Release Delay`, `Recover Time`
  - 비주얼 눌림/복귀.

### CrackedStonePlatform2D

Prefab: `Assets/Prefabs/Platforms/CrackedStonePlatform.prefab`

일반 착지는 안전합니다. 직접 내려찍기만 발판을 부숩니다.

- `Start Broken`
  - 처음부터 부서진 상태.
- `Break Only Once`
  - 한 번만 부서질지.
- `Break Delay`
  - 내려찍기 후 깨질 때까지 딜레이.
- `Platform Collider`
  - 실제 발판 콜라이더.
- `Visual Root`, `Broken Visual`
  - 정상/파괴 비주얼.
- `Break Vfx`, `Break Sfx`
  - 파괴 연출.
- `Continue Fall Speed`
  - 깨진 뒤 플레이어가 아래로 계속 떨어지는 속도.

### WaterMushroomBouncePlatform2D

Prefab: `Assets/Prefabs/Platforms/WaterMushroomBouncePlatform.prefab`

1단계는 통과, 물 투사체를 맞으면 2단계 발판이 됩니다. 큰 상태에서 불 투사체를 맞으면 다시 작아집니다.

- `Inactive Scale`, `Active Scale`
  - 작은/큰 버섯 크기.
- `Grow Time`, `Shrink Time`
  - 변화 시간.
- `Bounce Vertical/Horizontal Velocity`
  - 큰 버섯 상태에서 내려찍기 반동 속도.
- `Release Delay`, `Cooldown`
  - 반동 딜레이/쿨타임.
- `Platform Collider`
  - 2단계에서만 켜지는 발판 콜라이더.
- `Element Hit Trigger`
  - 물/불 투사체 감지 트리거.

### VineReleaseBridge2D

Prefab: `Assets/Prefabs/Bridges/VineReleaseBridge.prefab`

시작 상태는 덩쿨에 묶여 위로 들린 다리입니다. 불 투사체에 맞으면 아래로 떨어져 안정됩니다.

- `Bridge Collider Size/Offset`
  - 실제 밟는 발판 콜라이더.
- `Bridge Span`
  - 다리 가로 길이.
- `Rope Point Count`
  - 줄/매듭 지점 수.
- `Deck Y`
  - 해제 후 기본 발판 높이.
- `Release Drop Distance`
  - 해제 시 위 상태에서 아래로 내려오는 거리.
- `Deck Sag`
  - 가운데 처짐.
- `Handrail Height`, `Support Height`
  - 손잡이 줄/고정 줄 높이.
- `Deck Visual Size`
  - 발판 비주얼 크기.
- `Held Lift Height`
  - 시작 상태에서 들린 높이.
- `Released`
  - 현재 해제 상태.
- `Drop Duration`, `Drop Curve`
  - 해제 낙하 시간/곡선.
- `Release Wobble Amount/Frequency/Damping`, `Settle Time`
  - 해제 후 출렁임 크기/속도/감쇄/안정 시간.
- `React To Riders`
  - 캐릭터 탑승 시 눌림 반응.
- `Rider Sink Amount`, `Rider Wobble Amount`, `Rider Return Speed`
  - 탑승 눌림/출렁임/복귀.

### WaterBalancePlatform

Prefab: `Assets/Prefabs/Platforms/WaterBalancePlatform.prefab`

왼쪽/오른쪽 물 센서에 물 Blob이 닿으면 해당 쪽이 내려가고 반대쪽이 올라갑니다.

- `Left/Right Platform`
  - 움직일 발판 Transform.
- `Left/Right Water Sensor`
  - 물 감지 트리거.
- `Height Offset`
  - 올라가고 내려가는 높이.
- `Descend Speed`, `Ascend Speed`, `Neutral Speed`
  - 하강/상승/중립 복귀 속도.
- `Wet Hold Time`
  - Blob이 잠깐 끊겨도 젖은 상태로 유지하는 시간. 너무 들썩이면 키웁니다.
- `Water Layer Mask`
  - 물 Blob/Hazard 감지 레이어.
- `Carry Riders`
  - 발판 위 플레이어를 이동량만큼 같이 이동.

### SteamFlower2D

Prefab: `Assets/Prefabs/Platforms/SteamFlower.prefab`

물로 충전하고, 충전 상태에서 불을 맞히면 일정 시간 증기 상승 구역을 켭니다.

- `Charged Scale Multiplier`
  - 물 충전 시 봉오리 크기.
- `State Transition Time`
  - 상태 전환 시간.
- `Steam Duration`
  - 증기 유지 시간.
- `Steam Lift Speed`
  - 증기 안 플레이어 상승 속도.
- `Steam Zone Height/Width`
  - 증기 트리거 크기.
- `Steam Zone Trigger`
  - Active 상태에서만 켜지는 상승 구역.
- `Empty/Charged/Active Visual`
  - 상태별 비주얼.
- `Water Charge Vfx`, `Steam Burst Vfx`
  - 비워도 오류 없이 동작합니다.

## Water Flow

Prefab: `Assets/Prefabs/WaterFlow/WaterEmitter.prefab`

### WaterFlowSource

Blob을 계속 생성합니다. Blob은 직접 아래로 떨어지고, 표면에 닿으면 좌우로 흐르고, 끝을 만나면 다시 떨어집니다.

- `Spawn Blobs In Play Mode`
  - 플레이 중 Blob 생성.
- `Spawn Interval`
  - 생성 간격. 더 촘촘한 물줄기는 낮춥니다.
- `Blobs Per Spawn`
  - 한 번에 생성할 Blob 수.
- `Max Active Blobs`
  - 동시에 존재할 Blob 수. 렉이 있으면 낮춥니다.
- `Spawn Width`, `Spawn Jitter`
  - 생성 폭/랜덤 흔들림.
- `Blob Lifetime`
  - Blob 기본 수명.
- `Surface Lifetime Multiplier`
  - 표면 위에서 수명이 줄어드는 비율. 긴 바닥에서 물이 빨리 사라지면 이 값을 키웁니다.
- `Solid Mask`
  - 물이 부딪히고 흐를 지형 레이어.
- `Ground/Side/Ledge Probe Distance`
  - 바닥/옆/끝 검사 거리.
- `Gravity`, `Max Fall Speed`
  - 낙하 가속/최대 낙하 속도.
- `Spread Speed`
  - 표면 위 좌우 흐름 속도.
- `Slope Flow Mode`
  - `KeepCurrentDirection`: 현재 방향 유지.
  - `PreferDownhill`: 기울어진 표면에서 낮은 쪽 우선.
  - `ForceDownhill`: 기울어진 표면에서는 무조건 낮은 쪽.
- `Split On Flat Surfaces`
  - 평평한 표면에서 좌우 양갈래로 흐를지.
- `Blob Size`, `Blob Size Random`
  - 개별 Blob 크기.
- `Use Metaball Visual`
  - 가까운 Blob을 하나의 물 덩어리처럼 합쳐 보이게 함.
- `Metaball Cell Size`
  - 작을수록 부드럽지만 무거움.
- `Metaball Max Cells`
  - 렌더링 최대 셀 수. 성능 제한.
- `Metaball Update Interval`
  - 비주얼 업데이트 간격. 키우면 가벼워지지만 덜 부드러움.
- `Blob Is Hazard`
  - 불 캐릭터에게 위험한 물 판정.
- `Hazard Radius Scale`
  - Blob 위험 판정 반경.

## Darkness / Reveal

### DarkZone

어두운 구역 Trigger에 붙입니다.

- `Base Darkness Alpha`
  - 기본 어둠 강도.
- `Water/Fire Light Radius`
  - 물/불 캐릭터 빛 반경.
- `Water/Fire Reveal Strength`
  - 해당 빛이 어둠을 얼마나 밝히는지.

### FireLightSource / WaterCharacterLight

캐릭터나 등불에 붙입니다.

- `Radius`
  - 빛 반경.
- `Intensity`
  - 밝기.
- `Can Reveal Objects`
  - 불빛만 내부 발판/기믹 Reveal에 반응하게 하려면 물빛은 끕니다.

### RevealByFireLight

불빛에 닿았을 때만 보이는 발판/기믹에 붙입니다.

- `Hidden Alpha`
  - 안 보일 때 투명도.
- `Revealed Alpha`
  - 드러났을 때 투명도.
- `Use Partial Reveal`
  - 켜면 불빛이 닿은 부분만 드러남.
- `Partial Reveal Softness`
  - 드러나는 경계 부드러움.
- `Max Reveal Sources`
  - 동시에 반영할 불빛 수.

### ShadowRockSilhouette

어둠 속 큰 실루엣용입니다. 내부 기믹을 알 필요 없이 배경 덩어리 역할만 합니다.

- `Water Visible Radius`
  - 물 캐릭터가 가까이 왔을 때 실루엣이 보이는 거리.
- `Fire Visible Radius`
  - 불 캐릭터 기준 보이는 거리.
- `Use Partial Reveal`
  - 켜면 큰 바위도 빛 닿은 부분만 보임.

## Rescue

### FallRescueManager

씬에 하나만 두면 됩니다. 없으면 런타임에 자동 생성됩니다.

구조 요청은 이제 플레이어가 직접 합니다.

```csharp
fallRescueManager.RequestRescue(myControlledPlayer);
```

조건은 단순합니다.

- 구조 이벤트가 Idle 상태.
- 물/불 플레이어가 둘 다 살아 있음.
- 요청자가 물 또는 불 플레이어 중 하나.

### Skill Check

- `Water Skill Check Key`, `Fire Skill Check Key`
  - 물/불 체크 입력 키.
- `Skill Check Start Delay`
  - UI 시작 전 짧은 딜레이.
- `Base Skill Check Count`
  - 한 번의 구조에서 수행할 체크 수.
- `Require Each Player At Least Once`
  - 물/불이 최소 한 번씩 체크하게 할지.
- `Randomize Skill Check Order`
  - 체크 순서 랜덤.

### Rescue Pressure

- `Rescue Pressure`
  - 현재 구조 압력. 성공할수록 증가.
- `Max Rescue Pressure`
  - 최대 압력.
- `Base Needle Speed`
  - 스킬 체크 바늘 기본 속도.
- `Pressure Needle Speed Add`
  - 압력 1당 추가 속도.
- `Base Success Zone Size`
  - 기본 성공 구간 크기.
- `Pressure Success Zone Shrink`
  - 압력 1당 성공 구간 감소량.
- `Min Success Zone Size`
  - 성공 구간 최소 크기.

### Progress Reset / Mist

- `Stable Stand Time`
  - 둘 다 현재 잔향 안개 구역 밖에 있어야 하는 시간. 이 시간이 지나면 압력과 안개가 초기화됩니다.
- `Use Resonance Mist Zone`
  - 압력 안개 사용.
- `Resonance Mist Zone Prefab`
  - 별도 안개 프리팹. 비우면 런타임 파티클 생성.
- `Mist Zone Size`, `Mist Zone Offset`
  - 안개 구역 크기/위치.

구조 성공 후 압력 증가와 안개 생성.
구조 실패 후 떨어진 플레이어 착지/파트너 합류 시 압력 0, 안개 제거.
둘 다 생성된 잔향 안개 구역 밖으로 벗어나면 압력 0, 안개 제거.

## Hazards

### FallingEmberSpawner2D

Prefab: `Assets/Prefabs/Hazards/FallingEmberSpawner.prefab`

- `Ember Prefab`
  - 생성할 불씨.
- `Spawn Point`
  - 생성 위치.
- `Spawn Interval`
  - 생성 주기.
- `Start Delay`
  - 시작 딜레이.
- `Horizontal Random Range`
  - 좌우 랜덤 범위.
- `Initial Velocity`
  - 생성 시 초기 속도.

물 투사체를 맞으면 스포너가 중단됩니다.

### FallingEmber2D

Prefab: `Assets/Prefabs/Hazards/FallingEmber.prefab`

- `Fall Speed`
  - 낙하 속도.
- `Use Gravity`
  - Rigidbody 중력 사용.
- `Life Time`
  - 자동 제거 시간.
- `Ground Layer`
  - 닿으면 사라질 지형 레이어.
- `Impact Vfx`
  - 바닥 충돌 연출.
- `Kill Water Player`
  - 물 캐릭터 사망.
- `Ignore Fire Player`
  - 불 캐릭터는 안전.
