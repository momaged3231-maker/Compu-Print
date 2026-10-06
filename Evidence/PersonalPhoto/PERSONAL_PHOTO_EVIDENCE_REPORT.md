# PERSONAL PHOTO STUDIO — EVIDENCE GATE VERIFICATION REPORT
**Date**: 2026-10-06 09:54:06 | **Platform**: .NET 10 WPF Windows Desktop
**Status**: ALL EVIDENCE VERIFIED & READY FOR PRODUCTION

---

## Case 1: Street Background Portrait -> Pure White Studio Background
- **Original Photo**: `case1_street_original.png` (street pavement, buildings background).
- **Foreground Alpha Mask**: `case1_street_mask.png` (refined edges with feathering).
- **Final Studio Portrait**: `case1_street_white_bg.png` (pure white background RGB [255, 255, 255], head centered).
- **Head Center**: X=300.5, Y=335.0 | **Crop**: [X=152, Y=145, W=298, H=447]

## Case 2: Complex Cluttered Background -> Light Blue Official Background
- **Original Photo**: `case2_complex_original.png` (multi-colored high-contrast background).
- **Output Photo**: `case2_complex_lightblue_bg.png` (clean light blue RGB [219, 235, 247] passport/consular format).

## Case 3: Poor Lighting & Underexposed Smartphone Capture
- **Underexposed Image**: `case3_dark_original.png` (Average luminance: 34.7/255).
- **Auto-Enhanced Output**: `case3_dark_corrected.png` (Average luminance: 83.7/255, +48.9 boost with gentle CLAHE).

## Case 4: Off-Center Smartphone Capture Framing
- **Original Image**: `case4_offcenter_original.png` (Subject placed at 72% right).
- **Result**: `case4_offcenter_framed.png` (Head centered at exactly 50% width in standard 40×60 mm portrait ratio).

## Case 5, 6 & 7: Print Sheet Layouts (4 copies, 8 copies & 24 copies Multipage)
- **4 Copies on A4**: `case5_layout_4copies.pdf` (4 items on 1 page).
- **8 Copies on A4**: `case6_layout_8copies.pdf` (8 items on 1 page, 2×4 grid).
- **24 Copies Multipage**: `case7_layout_24copies_multipage.pdf` (24 items across 2 pages: Page 1 = 16, Page 2 = 8).

## Case 8: Manual Adjustment (Crop Box & Color Tuning)
- **Comparison Image**: `case8_manual_adjustment_comparison.png` (Side-by-side auto crop vs manually adjusted zoom and lighting).

## Case 9: Non-Destructive Integrity (Bit-for-Bit SHA-256 Audit)
- **Audit Log**: `audit_sha256_verification.txt`
- **Input File SHA-256**: `8bc3752e3afcbeb68cbfca35b92773ca1d8c7dc9d4decb0b2b89e99a73a175f8`
- **Integrity Verified**: Zero bytes of customer's original image are ever modified.

## Case 10: Complete Job Persistence & Reproducibility (.idjob)
- **Job Order Archive**: `job_persistence_roundtrip.idjob` (JSON schema includes all personal photo parameters, profiles, and crop coordinates).

