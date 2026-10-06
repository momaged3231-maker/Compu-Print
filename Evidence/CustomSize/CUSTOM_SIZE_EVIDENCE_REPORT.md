# Custom Size & Multi-Size Layout Engine Evidence Report

## Overview
This document proves the correctness and reliability of the **Custom Size Layout** workflow in `IdCardPrintShop`.

## Test Summary Table

| Case | Input Items | Paper | Expected Pages | Actual Pages | Items Placed | Overlap Check | Bounds Check | PDF Status | Result |
|---|---|---|:---:|:---:|:---:|:---:|:---:|:---:|:---:|
| **Case A** | 30 × 40 mm (×8) | A4 Portrait | 1 | 1 | 8 | PASS (0) | PASS (in bounds) | Valid 300 DPI | **PASS** |
| **Case B** | 50 × 60 mm (×4) | A4 Portrait | 1 | 1 | 4 | PASS (0) | PASS (in bounds) | Valid 300 DPI | **PASS** |
| **Case C** | 30×40 (×8), 50×60 (×4), 80×100 (×2) | A4 Portrait | 1 | 1 | 14 | PASS (0) | PASS (in bounds) | Valid 300 DPI | **PASS** |
| **Case D** | 80 × 100 mm (×5) | A4 Portrait | 1 (rot) vs 2 (no rot) | 1 (rot) vs 2 (no rot) | 5 | PASS (0) | PASS (in bounds) | Valid 300 DPI | **PASS** |
| **Case E** | 80 × 100 mm (×10) | A4 Portrait | 2 | 2 | 10 | PASS (0) | PASS (in bounds) | Valid 300 DPI | **PASS** |
| **Case F** | Manual Adjustment (Drag + Rotate) | A4 Portrait | 1 | 1 | 2 | PASS (0) | PASS (clamped) | Valid 300 DPI | **PASS** |

## Physical Dimension Verification
- **A4 Physical Sheet**: 210.0 × 297.0 mm (595.28 × 841.89 pt).
- **Safety Margin**: Default 3.0 mm (strictly respected on all edges).
- **Spacing**: Default 2.0 mm (strictly enforced between adjacent item boundaries).
- **Cut Marks**: 3.0 mm tick lines placed 0.8 mm outside item corners, non-intrusive.
- **Aspect Ratio**: Rotation transposes physical bounds and image mats without stretching.

## Persistence Verification
- `.idjob` format stores `CustomSizeItems` and `CustomSizeParameters`.
- Reloading restores all item dimensions, copies, orientations, and handles missing files with `Needs Review` status without crashing.
