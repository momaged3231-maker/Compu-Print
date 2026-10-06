# EVIDENCE GATE COMPREHENSIVE VERIFICATION REPORT
Generated on: 2026-10-06 09:54:01

## Case A: Single Card with Large Margins
Detection Case: SingleCard
Cards Count: 1
IsConfident: True
Message: تم اكتشاف بطاقة واحدة بنجاح.
Card #1 (بطاقة 1) [Role: Front]:
  Corner 1 (Top-Left):     (607.0, 420.0)
  Corner 2 (Top-Right):    (1089.0, 417.0)
  Corner 3 (Bottom-Right): (1095.0, 721.0)
  Corner 4 (Bottom-Left):  (608.0, 725.0)
  Width (Top/Bottom avg):  484.5 px
  Height (Left/Right avg): 304.5 px
  Aspect Ratio:            1.591
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)


## Case B: Front + Back Side-by-Side
Detection Case: TwoCards
Cards Count: 2
IsConfident: True
Card #1 (بطاقة أ (الوجه المقترح)) [Role: Front]:
  Corner 1 (Top-Left):     (247.0, 354.0)
  Corner 2 (Top-Right):    (710.0, 352.0)
  Corner 3 (Bottom-Right): (715.0, 646.0)
  Corner 4 (Bottom-Left):  (248.0, 650.0)
  Width (Top/Bottom avg):  465.0 px
  Height (Left/Right avg): 295.0 px
  Aspect Ratio:            1.576
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)
Card #2 (بطاقة ب (الظهر المقترح)) [Role: Back]:
  Corner 1 (Top-Left):     (886.0, 356.0)
  Corner 2 (Top-Right):    (1348.0, 351.0)
  Corner 3 (Bottom-Right): (1355.0, 646.0)
  Corner 4 (Bottom-Left):  (888.0, 650.0)
  Width (Top/Bottom avg):  464.5 px
  Height (Left/Right avg): 294.5 px
  Aspect Ratio:            1.577
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)


## Case C: Front + Back Stacked Vertically
Detection Case: TwoCards
Cards Count: 2
IsConfident: True
Card #1 (بطاقة أ (الوجه المقترح)) [Role: Front]:
  Corner 1 (Top-Left):     (267.0, 119.0)
  Corner 2 (Top-Right):    (730.0, 117.0)
  Corner 3 (Bottom-Right): (735.0, 411.0)
  Corner 4 (Bottom-Left):  (268.0, 415.0)
  Width (Top/Bottom avg):  465.0 px
  Height (Left/Right avg): 295.0 px
  Aspect Ratio:            1.576
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)
Card #2 (بطاقة ب (الظهر المقترح)) [Role: Back]:
  Corner 1 (Top-Left):     (268.0, 558.0)
  Corner 2 (Top-Right):    (728.0, 556.0)
  Corner 3 (Bottom-Right): (735.0, 851.0)
  Corner 4 (Bottom-Left):  (268.0, 855.0)
  Width (Top/Bottom avg):  463.5 px
  Height (Left/Right avg): 296.0 px
  Aspect Ratio:            1.566
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)


## Case D: Back + Front Reversed Order
Detection Case: TwoCards
Cards Count: 2
Reversed order handling: The system detects both regions independently.
Card #1 (بطاقة أ (الوجه المقترح)) [Role: Front]:
  Corner 1 (Top-Left):     (757.0, 349.0)
  Corner 2 (Top-Right):    (1240.0, 347.0)
  Corner 3 (Bottom-Right): (1245.0, 651.0)
  Corner 4 (Bottom-Left):  (758.0, 655.0)
  Width (Top/Bottom avg):  485.0 px
  Height (Left/Right avg): 305.0 px
  Aspect Ratio:            1.590
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)
Card #2 (بطاقة ب (الظهر المقترح)) [Role: Back]:
  Corner 1 (Top-Left):     (147.0, 350.0)
  Corner 2 (Top-Right):    (629.0, 347.0)
  Corner 3 (Bottom-Right): (635.0, 651.0)
  Corner 4 (Bottom-Left):  (148.0, 655.0)
  Width (Top/Bottom avg):  484.5 px
  Height (Left/Right avg): 304.5 px
  Aspect Ratio:            1.591
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)


## Case E: Skewed Perspective Card
Detection Case: SingleCard
Cards Count: 1
IsConfident: True
Card #1 (بطاقة 1) [Role: Front]:
  Corner 1 (Top-Left):     (279.0, 217.0)
  Corner 2 (Top-Right):    (882.0, 160.0)
  Corner 3 (Bottom-Right): (822.0, 685.0)
  Corner 4 (Bottom-Left):  (205.0, 622.0)
  Width (Top/Bottom avg):  612.9 px
  Height (Left/Right avg): 470.1 px
  Aspect Ratio:            1.304
  Confidence:              78%
  Rotation Quarter Turns:  0 (Total: 0°)
Rectified Result Dimensions: 626 × 395 px
Rectified Aspect Ratio: 1.585


## Case F: Challenging Detection Image (Low contrast / shadow)
Detection Case: SingleCard
IsConfident: True
Message: تم اكتشاف بطاقة واحدة بنجاح.
Card #1 (بطاقة 1) [Role: Front]:
  Corner 1 (Top-Left):     (359.0, 301.0)
  Corner 2 (Top-Right):    (838.0, 299.0)
  Corner 3 (Bottom-Right): (840.0, 598.0)
  Corner 4 (Bottom-Left):  (361.0, 600.0)
  Width (Top/Bottom avg):  479.0 px
  Height (Left/Right avg): 299.0 px
  Aspect Ratio:            1.602
  Confidence:              85%
  Rotation Quarter Turns:  0 (Total: 0°)


## PDF Physical Dimensions Verification
- A4 PDF Page 1: Width=210.00 mm (Expected: 210.00), Height=297.00 mm (Expected: 297.00) -> Matched: True
- A5 PDF Page 1: Width=148.00 mm (Expected: 148.00), Height=210.00 mm (Expected: 210.00) -> Matched: True
- A3 PDF Page 1: Width=297.00 mm (Expected: 297.00), Height=420.00 mm (Expected: 420.00) -> Matched: True

## Non-Destructive SHA-256 Integrity Verification
- Original File: C:\Users\moham\OneDrive\سطح المكتب\P\Samples\01_front_and_back_side_by_side.jpg
- SHA-256 Before Operations: 661C8CF10C6D46D431328DDF553FB8E3822761522DA194CB362A2324BD9CC901
- SHA-256 After Operations:  661C8CF10C6D46D431328DDF553FB8E3822761522DA194CB362A2324BD9CC901
- Bit-for-bit Identical:     True

## Job Persistence & Reproducibility (.idjob)
- Order Number Restored: True
- Customer Name Restored: True
- Copies Count Restored: True
- Template ID Restored: True
- Input Files Restored: True
- Card Corners Exactly Restored: True
- Regenerated PDF from audit file exists: True (Size: 30315 bytes)

## Manual Adjustment Logic Verification
- Moving corner 0 -> Updated VM Corner0X and Corner0Y immediately.
- Rotate 90 CW + Flip 180 -> RotationQuarterTurns = 3 (270°).
- Fine rotation = 3.0°, Safety Margin = 2.0% -> Applied to CardRegion.
- Apply() updates CardRegion and marks IsManualAdjusted = true.

