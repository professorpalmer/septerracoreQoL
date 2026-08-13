Septerra Core Quality of Life (modern Win11 rebuild)
====================================================

You need a purchased Steam (or GOG) copy of Septerra Core.
This patch does NOT provide game files. Never commit *.db / game exe / saves.

What this stack fixes
---------------------
- Exclusive DirectDraw fullscreen that plunges desktop resolution on Win11
- Ugly alt-tab / broken OBS Game Capture on exclusive surfaces
- Crash / combat F / auto-run via Albeoris inject (MIT upstream, 2019 Core.dll)
- Combat F advances one party ATB bar and does not also grant an enemy a turn
- Optional FMV via QuickTime codecs from the game qt\ folder

Architecture (2026)
-------------------
PRIMARY display: dgVoodoo2 (MS\x86 DDraw.dll) — borderless, 4:3, streamable
QoL / inject:    Albeoris Launcher (Launcher\Septerra.exe)
DEFAULT launch:  Launch.bat -> run . -r   (auto-run ON, movies ON)
DxWnd:           OPTIONAL fallback only (legacy dxwnd\ folder). Do not stack on dgVoodoo.

Quick deploy (this machine / any Steam install)
----------------------------------------------
1) Install Septerra Core on Steam. Launch once (creates registry + septerra.ini).
2) Extract dgVoodoo2 (tested: 2.86.4) somewhere, e.g. Downloads\dgVoodoo2_86_4
3) From this repo:

   powershell -ExecutionPolicy Bypass -File .\scripts\deploy-to-steam.ps1

   Or pass -GameRoot / -DgVoodooRoot if paths differ.
   Accept the UAC prompt for QuickTime (SysWOW64 copy).

4) Play via Steam Play or Launch.bat — both start Albeoris inject (F-skip, auto-run, movies).

Launcher\Septerra.exe in this repo is already patched to inject septerra.bin
(not the Steam Play trampoline). deploy-to-steam.ps1 re-applies that patch.
Keep the 2019 Septerra.Core.dll; combat F is a surgical IL patch
(tools\PatchAtbOneBar.cs), not a rebuild from current Albeoris GitHub.

Manual deploy
-------------
1) Copy Launcher\ and Launch.bat (+ optional Unpack/Convert bats) into the Steam game folder.
2) Copy dgVoodoo2 MS\x86\DDraw.dll, D3DImm.dll (and D3D8/D3D9) + dgVoodoo\dgVoodoo.conf into the game root.
3) Run qt\QuickTimeInstaller.bat as Administrator once (FMV).
4) Start Launch.bat

Launch.bat flags
----------------
  .\Launcher\Septerra.exe run . -r

- Remove -r to disable default run (shift to run becomes vanilla behavior depending on game).
- Add -M only if you must skip movies (not recommended once QT is installed).

Key remapping
-------------
Edit septerra.ini in the game root (created after first legal launch).

Window size / scaling
---------------------
Prefer editing dgVoodoo.conf (Resolution / ScalingMode) or run dgVoodooCpl.exe.
Dragging a raw exclusive window is the wrong tool — that is why DxWnd was used historically.

OBS
---
Use Game Capture or Window Capture on the game window. Display Capture is a last resort.

Steam Verify Integrity
----------------------
Will delete dgVoodoo drop-ins and may remove overlay files. Re-run deploy-to-steam.ps1 after verify.

Credits / provenance
--------------------
- Albeoris Septerra tools — MIT (https://github.com/Albeoris/Septerra)
  Combat F is an IL patch of the 2019 BattleDispatcher (tools\PatchAtbOneBar.cs).
- dgVoodoo2 — Dege (https://dege.freeweb.hu/dgVoodoo2/)
- Legacy dxwnd\ tree — optional SourceForge DxWnd-based pack (historical)

4:3 size examples (dgVoodoo Resolution)
---------------------------------------
640x480, 800x600, 1024x768, 1280x960
