@echo off
chcp 65001 > nul
title تشغيل برنامج طباعة الكروت والوثائق
echo جاري تشغيل برنامج IdCardPrintShop...
cd /d "%~dp0IdCardPrintShop\bin\Debug\net10.0-windows"
start "" "IdCardPrintShop.exe"
