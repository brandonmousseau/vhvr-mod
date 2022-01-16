[33mcommit 564e8dbf8efcf806817c3e71fd8f7ea98ed7d8cf[m[33m ([m[1;36mHEAD -> [m[1;32mVRHud_enhancements[m[33m)[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Aug 26 23:19:00 2021 +0200

    fix

[33mcommit 403d1372f0bd4f9718320f7093b36f7b789f494b[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Aug 26 22:02:06 2021 +0200

    enhancements

[33mcommit 086b0a9613c2d982089a828a0c00a59d7cd3f579[m[33m ([m[1;31morigin/VRHud[m[33m, [m[1;32mVRHud[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Aug 1 21:27:08 2021 -0700

    Add stamina to VR Hud
    
    This change adds the Stamina bar to the new VR HUD. It is customizable,
    allowing the player to pick it's location independently of the health
    panel.

[33mcommit e0a6308e73ba391d57fac92c214232d3527f4334[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Aug 1 13:05:11 2021 -0700

    Add option to hide Hotbar
    
    Hotbar is mostly useless when using VR controls. Added a default on
    option to disable it. Users can make it visible if desired.

[33mcommit 30f7924b3f7e65e107e0070d65ca6037c3c1d8bc[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Jul 31 15:46:03 2021 -0700

    Move HealthPanel to new VRHUD
    
    This change adds a new VR HUD which is used to display the healthbar.
    Users can optionally select between a camera locked HUD or a wrist-watch
    based hud. Options are included to reposition and scale it.
    
    This change only moves the health panel, but future changes will move
    other HUD components, such as stamina and GPower indicators.

[33mcommit 37b53ecda8681ca47ba0068d3307d6f17b8d01bc[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Thu Jul 29 15:37:31 2021 +0200

    Fix cursor position when using texture packs with bigger cursors

[33mcommit 70712b6ef56b0f742eabd75a07df4d9ab37cb2f9[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 13:42:22 2021 -0700

    Add missing early return.

[33mcommit 2f9336b0b6d256e8807732e3f37d309e2365a121[m[33m ([m[1;33mtag: v0.7.0[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 12:53:01 2021 -0700

    Make diagram text white for readability
    
    fixes #98

[33mcommit e79da3a3b3a704a476e282db912cbf51b05bf89e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 11:01:06 2021 -0700

    Add nonVR mode build
    
    Work in progress.
    
    This adds a build flavor that forces nonVR mode to be on always. This
    allows for a separate companion mod to be installed by users who want to
    play with VR players that is easier to install. The goal is to be able
    to distribute a simple DLL without any other dependencies for non-VR
    players, enabling easier install, including with a mod manager like
    Vortex. This version doesn't work yet though.

[33mcommit eb9f9d6e02c5d7d77d90c748a630312580679c6c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 10:44:57 2021 -0700

    Update app key for Index and Touch bindings
    
    This should match the steam APP id.

[33mcommit eeb74e69244d73a97555becc7c04fbe3a965cef1[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 10:39:10 2021 -0700

    Increment version number to 0.7.0

[33mcommit 66367898daf08eef9f1abb267bac32fc10c7a0b3[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jul 25 10:38:35 2021 -0700

    Fix steam APP id issue
    
    Fixed an error that one user had related to loading the steam APP id.

[33mcommit a32cfb7eadca3a6a5a4180db9d090e153c44e9cb[m[33m ([m[1;32mmaster[m[33m)[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jul 25 17:11:39 2021 +0200

    fix non-vr player exclusion in postprocessing

[33mcommit 6da11e650b6b5c22d1ff8a782f656696094daf47[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jul 25 00:34:01 2021 +0200

    fix: activate config changes only at save

[33mcommit ecebbfca9f352d14dc7c0e7f411b4d4ecd43a586[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jul 24 23:07:31 2021 +0200

    move tooltip to the bottom, to avoid resolution problems

[33mcommit 7092108110491715918b17a93db68d2d44f9e778[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jul 24 18:08:12 2021 +0200

    fix font size of tooltip

[33mcommit 455780818bbee207a800d4e10ce3962befaa747b[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jul 24 16:40:34 2021 +0200

    ingame config settings (#109)
    
    ingame config settings + fix scrolling

[33mcommit d19a65707826c87e0c5d5de9921264a2cbf25e63[m
Author: artum <jacopo.libe@gmail.com>
Date:   Sat Jul 24 14:01:55 2021 +0000

    Fix water bodies disappearing under the skybox (refactor PostProcessing fixes) (#110)
    
    * Fix for ocean not drawing over skybox
    
    * Properly set render target
    
    * Properly manage command buffer
    
    * Refactored code to divide postprocessing components from patches, moved water fix to postprocessing component
    
    * Poll settings to allow on-the-fly sharpen changes
    
    * Revert dumb changes

[33mcommit 66a118c338262ffbd5b5c79a44f3060ade842dd1[m
Author: artum <jacopo.libe@gmail.com>
Date:   Sat Jul 24 14:01:31 2021 +0000

    Fix distant fog calculations (#111)
    
    * Fixed fog issues
    
    * Account for camera translation
    
    * Removed unecessary transform

[33mcommit 701126b2b265c7d954e82394ce597db0ae512ffd[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jul 22 03:45:52 2021 +0700

    Add Spear Enhancement (Clean files branch) (#102)
    
    Spear Enhancements

[33mcommit 4138cc6cd38c436e321ffa398cac41f5ef4565a8[m
Author: artum <jacopo.libe@gmail.com>
Date:   Fri Jul 16 15:02:18 2021 +0200

    Basic roomscale (#96)
    
    * Roomscale experiment
    
    * Handle recentering
    
    * Animation support
    
    * Prevent offset building up over time
    
    * Use SmoothDamp for step animation smoothing
    
    * Tweak IK to not look akward
    
    * Refactor
    
    * Better way of handling character movement
    
    * Poperly handle roomscale movement on ships
    
    * Back to rigidbody instant movement, moved vrik settings also to networked players
    
    * Consider ground normals when moving the player
    
    * Ship improvements
    
    * Fade to black and attachment recentering
    
    * More general player attachment point handling
    
    * Longer fading and back to a less error prone rotation method
    
    * Fixes to centering after reverting to yaw rotation handling
    
    * Streamline fade to black, add checks for nonvr players, transpile applygroundforces
    
    * Smaller collider test, use Valve interaction system instead of saving hmd transform
    
    * Removed collider edits, removed transpiler for simplicity
    
    * More stable HUD rotation
    
    * Avoid wrong GUI snap after recentering
    
    * Consider attachment point movement when calculating roomscale motion discrepancy
    
    * Fixed merge issues

[33mcommit f9298ac37b87a2a4e456722ce7c547ff46c0426e[m
Author: artum <jacopo.libe@gmail.com>
Date:   Mon Jul 12 23:43:30 2021 +0200

    Remove repeated calls to FindObjectsOfType in update functions across the mod (#104)

[33mcommit 89f3eebb4fa00b5381bac7670127557f05b43e1e[m
Author: artum <jacopo.libe@gmail.com>
Date:   Mon Jul 12 23:33:10 2021 +0200

    Disable camera culling (#103)

[33mcommit 394923a0d534931dade2abd55d8c4105bf7fea03[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jul 12 23:23:47 2021 +0200

    remove bow collider + fix hand rotations after holster + fix max arrow speed

[33mcommit 72a264fbda2631b7b013cdb1d6f0949684d44467[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jul 12 01:30:51 2021 +0200

    fix seperate holsters

[33mcommit 95855d017e45ec07de46deb4ba24f854ebdb07fa[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Fri Jul 9 21:47:58 2021 +0200

    Properly check if actionset is initialized

[33mcommit 116885667b71adae4c643598f38d8e03e9dffcfd[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Fri Jul 9 19:31:16 2021 +0200

    Added slider fine control with right stick

[33mcommit b9211ec10a5f7708beda3ea4228cd1562c7a84e5[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jul 11 02:18:02 2021 +0200

    fix quickswitch unequip refresh

[33mcommit c152275be7703e590fe4b34994aa58fc7446295e[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jul 11 02:02:57 2021 +0200

    Bow drain stamina on fast draw + arrow particle size configurable

[33mcommit 6317b9213fe4c233369cba5755167e4b397639e1[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Thu Jul 8 20:40:31 2021 +0200

    Fixed some exceptions caused by gameobject initializations in constructors

[33mcommit f2b2fe25fedd339c33fbb34f9583d4696c0c4772[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Tue Jul 6 12:19:00 2021 +0200

    enable weapon red outline for cooldown of non-horizontal attacks + deactivate spear chitin collider as it has no melee attack

[33mcommit 78d457463fedf9ea2f4f6a369b7d8015867e25e9[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Sun Jul 4 01:01:30 2021 +0200

    Fade to black on some events and during loading screens

[33mcommit e687e5a419fedcd0f76b0c1ba96c60e20b4ca460[m[33m ([m[1;31morigin/feature-2[m[33m, [m[1;31morigin/feature-1[m[33m)[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Sat Jul 3 16:11:47 2021 +0200

    Changed trigger to buttons to avoid full pulls for basic interactions

[33mcommit 51f842d32c9525d9c7ff08d5cc35a459263af2c6[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Sat Jul 3 15:11:14 2021 +0200

    Moved vive wand bindings away from system buttons

[33mcommit ec87c858d14b4636833ddaf2ee9a5d343f292494[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Jul 3 11:23:40 2021 -0700

    Use publicized assemblies
    
    This change updates the reference to the assembly_valheim.dll to a
    publicized version with open access to all private members. This is a
    performance optimization step since FieldRefAccess proved to be
    expensive.
    
    This requires that the publicized dll be generated.
    
    The change also enables Running of "Unsafe Code Blocks" in the csproj
    build file, which is required to read private fields after compiling
    against the public version of the assembly.

[33mcommit a1f1d555283f81cd89faff580b04b6692cba701b[m[33m ([m[1;33mtag: v0.6.0[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 21:04:32 2021 -0700

    Update version number

[33mcommit 63fc7eb700a17062ddf317758a81b97428aa7be8[m
Merge: 338eab9 1db495f
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Fri Jul 2 20:53:31 2021 -0700

    Merge pull request #93 from artumino/head_rotation_fixes
    
    Head rotation fixes

[33mcommit 338eab9d4b4fb1fab6356844adb8908399810858[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 20:29:10 2021 -0700

    Make run optionally a toggle
    
    Users complained that it wasn't possible to sprint and jump because
    their thumb was busy holding the run forward and not able to press the
    jump button. This adds a default on option to make run a toggle instead,
    which frees up the thumb.

[33mcommit 1a55f626c5e8b122facaa31c31733e65be9ec9e3[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 20:24:36 2021 -0700

    Update controller diagram

[33mcommit 1db495fded87049209cc06dab66fa4c2164db562[m[33m ([m[1;31morigin/head_rotation_fixes[m[33m)[m
Merge: 3f14cdd e5b4304
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Sat Jul 3 05:22:29 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod into head_rotation_fixes

[33mcommit 3f14cddfc8702280329906636bf2ff08ad063f78[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Sat Jul 3 05:13:49 2021 +0200

    Fixed hmd rotation also causing movement while off-center

[33mcommit e5b4304fab1f918af50b955c7a7ffa0b86cdbead[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 19:53:17 2021 -0700

    Use actual time delta to control rotation speed
    
    Previously, the alt piece rotation speed was being based on a fixed
    number of updates occuring, essentially tying the speed to framerate.
    This fixes that and makes it based on actual time delta instead as well
    as exposes a config to allow the user to tweak the delay if they want.

[33mcommit fc8683fd023fa98f5198e1a2a64ceb8c0f8c7c46[m
Merge: 171924e 0280376
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jul 3 00:03:50 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit 171924e18cf6fb41fa52b0e40656b77c6fa405be[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jul 3 00:03:41 2021 +0200

    another stagbreaker fix

[33mcommit 0280376cca78eb5bb77c04f1a694079932bf3b5b[m
Merge: 1e19181 7d02b22
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Fri Jul 2 14:14:40 2021 -0700

    Merge pull request #89 from brandonmousseau/crouch_patch
    
    Update crouch logic

[33mcommit 1e191811f370d92a6140d16e877f949082cf5289[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 13:25:14 2021 -0700

    Make near clip configurable
    
    Some users reported being able to see the player nose, seems to be
    limited to some HMDs. This change adds a config option to adjust near
    clip plane that should help those players. Also moved the setting to the
    camera initialization instead of vrik add.

[33mcommit f90febde4ff3e9e3ca9680058ff549601edcd44f[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 13:04:27 2021 -0700

    Add log statement for debugging people's logs.

[33mcommit 11e6227c431ca7ff7795b5b40b89af446318137e[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Jul 2 21:38:28 2021 +0200

    Nonvr player test (#90)
    
    non-vr player works

[33mcommit 7d02b221d771d0c5e753421dbf0515dec76ddbe6[m[33m ([m[1;31morigin/crouch_patch[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 10:55:36 2021 -0700

    Update crouch logic
    
    Refactors crouch logic to prevent some issues with the camera not being
    updated properly when crouching/uncrouching sometimes.

[33mcommit e89211df03ad0d0725a2edd2f88444809610a11c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 07:38:56 2021 -0700

    Fix condition for crouch height adjust

[33mcommit 8dff19f20fd857024aca2f2ae20d7038040e6f0c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 07:29:58 2021 -0700

    Refactor control patches for sneaking and equipping

[33mcommit c18ccadb726e6e5ebda69a33e42d0ae938f17a31[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jul 2 07:11:09 2021 -0700

    Update crouch height adjust
    
    Adjust crouch height whenever the player is crouching using
    non-Roomscale crouching input.

[33mcommit fd175f9978070c7949fd313122578def7e49d864[m
Merge: 8147ab5 44c8b6e
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Fri Jul 2 07:08:34 2021 -0700

    Merge pull request #84 from Aceship/feature-1
    
    Add Roomscale Sneak

[33mcommit 44c8b6e74787795e789a12a203711bbf10a563ec[m
Merge: 87f029f 8147ab5
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Fri Jul 2 07:07:01 2021 -0700

    Merge branch 'master' into feature-1

[33mcommit 87f029fbec885f1bd9589da9fd59a5fa23b2e867[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jul 2 15:42:21 2021 +0700

    Formatting fix & changes
    
    - Add else statement if roomscale sneak option turned off
    - whitespace formatting change
    - un-nested if statement

[33mcommit 8147ab5e26b44ebd9b46f2b90d5f841c1cc61591[m
Merge: dff051e 32de369
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Thu Jul 1 16:33:48 2021 -0700

    Merge pull request #87 from artumino/taa_additional_fixes
    
    Additional TAA fixes and configurable sharpen ammount

[33mcommit dff051ee8891d727713f35848d56bf068cbfe41f[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jul 1 16:20:08 2021 +0200

    push missing AssetBundle stuff

[33mcommit b18cbff54dcf7bef0116fb694a68b99c0b1bc9fa[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jul 1 01:21:19 2021 +0200

    add map.png

[33mcommit e49a58dafc297411719c0ce4f25fa6c3e61f6e61[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jul 1 01:17:01 2021 +0200

    add map as quick actions element

[33mcommit 321bc878dbf0f4e743e612b3f680323508253c8f[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 30 23:08:57 2021 +0200

    lower dmg multiplier for hitting multiple enemies at once

[33mcommit af96db7b1598dc9d68deedf411a0f60f8dc25b8c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 30 21:23:10 2021 +0200

    make areattack only hit one target

[33mcommit 7db8bccb13697ebad74c8248c39451fc1f8c8932[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 30 21:08:57 2021 +0200

    fix Stagbreaker and other area attacking weapons. make pickaxes only hit one target. some bug fixes

[33mcommit 32de369eabdfb9fabeaac956c1620d30b40c426b[m[33m ([m[1;31morigin/taa_additional_fixes[m[33m)[m
Author: artum <jacopo.libe@gmail.com>
Date:   Wed Jun 30 15:58:37 2021 +0200

    Fix typo

[33mcommit d3e8ee822c574c28fceeaef817c4a5350496f387[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Wed Jun 30 15:53:26 2021 +0200

    Added configurable sharpen ammount to TAA

[33mcommit 7470faa61337b062c62e538874418db9666de7ec[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Wed Jun 30 15:35:53 2021 +0200

    Solved issues caused by player deaths

[33mcommit a730fc0fdc6c062514575ab00a3d72a416be9db7[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Wed Jun 30 12:51:37 2021 +0700

    Change the whole logic for roomscale sneaking

[33mcommit c9210796adb3c451bedc434bc582e1809ffd2635[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 30 01:32:55 2021 +0200

    fix hit direction

[33mcommit 58fe7fabc4c95e408d43de6afa34447aa6119826[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 30 00:51:46 2021 +0200

    add config to disable the need of weapon speed momentum

[33mcommit fc6bf56633caab787a3d651bc41132432438d0e4[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Tue Jun 29 23:29:49 2021 +0200

    improve vr keyboard

[33mcommit 7f15b574c10fc000a86784b0a188f90603cb98c0[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 23:24:44 2021 +0700

    Changed variable name & add 5% deadzone to sneaking

[33mcommit a46a2b48691ff02f7d355303e74cab6ba49125cb[m
Merge: e1ea11d ad283af
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Tue Jun 29 09:06:47 2021 -0700

    Merge pull request #85 from artumino/taa
    
    Fix Temporal Antialiasing

[33mcommit ad283afa8d26497824aa1d2b0232d9f4a172c1ab[m[33m ([m[1;31morigin/taa[m[33m)[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Tue Jun 29 17:54:43 2021 +0200

    Formatting and removed useless changes

[33mcommit 72e32af1ed98f541de01d6bc206d4044e2103146[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Tue Jun 29 17:48:53 2021 +0200

    Substitute TAAComponent with a VR compatible version

[33mcommit 77f01a2aa207f96e0c7470d52b28796875a48da8[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 22:18:22 2021 +0700

    Some Changes on config and eye height
    
    + Change height from camera y pos to eye height
    + moved the config logic to inside function (CheckSneakRoomscale())
    + Changed config from height in cm to percentage

[33mcommit 1dce570a67ef174ae390ecf776abd7476a959392[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 21:45:25 2021 +0700

    Roomscale Sneak Enhancement
    
    + Allow roomscale sneak while still having  non roomscale sneak
    + Allow running while roomscale sneak (and sneak again after finish running)
    + Allow sneak from non-roomscale to roomscale
    + Roomscale sneak stamina check

[33mcommit cf6b60e3bde01d48cba7b4e953e0cb8d1758927b[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 13:19:13 2021 +0700

    Add Roomscale Sneak
    
    + Add Roomscale sneak
    + Add Roomscale sneak option (Enabling and Height of sneaking)
    
    - currently disable running while roomscale sneaking
    - still can attack while sneaking

[33mcommit e1ea11d7486dd58c19ff3a40316d795c5f8b7ba9[m
Merge: a532d82 1c16017
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Mon Jun 28 12:56:19 2021 -0700

    Merge pull request #71 from Aceship/master
    
    Fishing enhancement + Build tool + Stab Mechanic + Shield Parry Mechanic + Spear Throw enhancement

[33mcommit 1c16017dbc4a475dbf143a89c58e4b5f226d3501[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 02:10:48 2021 +0700

    minor const fix

[33mcommit dce0673dc861d62241dcef875c938cc94add535a[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 02:08:12 2021 +0700

    change to const variable
    
    -change most of the important number to constant variable

[33mcommit b6882177eb69ca909a4cf688e9132d250ad357f8[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 01:18:18 2021 +0700

    Revert accidental change

[33mcommit 640cc29cbd85ee5eae72683b9605fc7829dab010[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Tue Jun 29 00:06:31 2021 +0700

    Spear throwing fix
    
    - change to use local position of hand

[33mcommit cc6701ebe2d582a0c1fed5d6725e1308788ef37a[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Mon Jun 28 23:51:29 2021 +0700

    Stabbing fix
    
    - Change stabbing mechanic to use local position for better accuracy

[33mcommit a532d8213f8e33c53a0819a94be774b2fd45086d[m
Merge: e35a2a2 be27642
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Mon Jun 28 09:25:39 2021 -0700

    Merge pull request #82 from artumino/passwordfield_input
    
    Add vr keyboard to server password field and move caret appropriately

[33mcommit be27642db2d854569c4eb428a207cb18676cf6c3[m[33m ([m[1;31morigin/passwordfield_input[m[33m)[m
Author: Jacopo Libè <jacopo.libe@gmail.com>
Date:   Mon Jun 28 16:51:25 2021 +0200

    Added steamvr keyboard to server password fields

[33mcommit 8830b48d87be0300f1b170b7f19af561cfe8a7b2[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Mon Jun 28 21:06:11 2021 +0700

    Shield Parry fix
    
    - Fix shield can always parry while moving / turning
    - Add different parrying vibration (temp)

[33mcommit 18a5f9cd55c4823b55b92307d4651bb389abae78[m
Merge: 29f915a e35a2a2
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Mon Jun 28 12:26:53 2021 +0700

    Merge branch 'brandonmousseau:master' into master

[33mcommit e35a2a22f4920839984c2148bd2be412621b15ef[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Jun 26 21:30:10 2021 -0700

    Enable snap turn
    
    This change adds the option to use snap turning. Two different flavors
    exist, smooth snap and non-smooth snap.
    
    Smooth snap will increment to the snapped angle over the course of a few
    frames to give the sensation of turning, whereas non-smooth snap mode
    will immediately increment by the full snap angle all at once.
    
    The snap angle amount and the smooth snap speed are configurable.
    
    fixes #64

[33mcommit 29f915acce324219348c3f71ca0facbf8ebf38cf[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Sat Jun 26 18:25:04 2021 +0700

    Spear throwing enchancement
    
    - Spear should now throw more accurately
    - Added some throw speed modifier for fast throw to reach higher speed easier

[33mcommit c16960dae884d7c6a7c9b27c23e8157a221bb84f[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Sat Jun 26 10:31:21 2021 +0700

    Add Shield Parry mechanic
    
    - add shield parry mechanic, parry by swinging it towards enemy

[33mcommit 24af4d639573c12b572415e7b29562300ef8dcb0[m
Merge: c10233b dc7fc49
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Sat Jun 26 03:28:09 2021 +0700

    Merge branch 'brandonmousseau:master' into master

[33mcommit dc7fc4947dc34d796078b4cad73a49a1048cbec0[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Jun 25 17:59:13 2021 +0200

    remove sphere collider

[33mcommit c10233b672e6be610fa1400f8934da9401338cb5[m
Merge: ead8d6b bb1ee24
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jun 25 22:23:59 2021 +0700

    Merge branch 'brandonmousseau:master' into master

[33mcommit bb1ee24143755f6e0fcb17da00c16e023b380e7f[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Jun 25 17:09:29 2021 +0200

    rename

[33mcommit 01d01a0526b342d661da99dddca3da983030e96b[m
Merge: 18520d6 3e5240c
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Jun 25 16:54:31 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit 18520d6d003fbdb993724e5c33b0b179e76f0301[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Jun 25 16:54:10 2021 +0200

    implement virtual keyboard + visual outline for fist attacks

[33mcommit ead8d6b9d221a1e850212ce89b34d8a0e462eab5[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jun 25 20:22:02 2021 +0700

    Add Stabbing mechanic
    
    - Stabbing mechanic

[33mcommit d075a2f03a396f80c381187d21eed25dcc8d966c[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jun 25 13:13:07 2021 +0700

    Fishing enhancement + Build tool
    
    - Change fishing to works like spear (sheath on ungrab,doesnt throw if hands doesnt move much)
    
    - Change building tool to use ungrab to sheath
    - Change grab to right hand  for building tool rotation modifier

[33mcommit 3e5240c1a819476851390e05adb24777c17fb603[m
Merge: dc36c27 d9c08f0
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Thu Jun 24 12:43:02 2021 -0700

    Merge pull request #63 from Aceship/master
    
    Quickmenu unlock position from head rotation and angle configuration

[33mcommit d9c08f0225fb29afe4a6810a5e06d7a54fea969a[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jun 25 02:22:21 2021 +0700

    Minor change to check spear

[33mcommit 1c8f7d7485f2e8115ce646df6700712ac4073879[m
Merge: 42adb53 dc36c27
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Fri Jun 25 01:44:47 2021 +0700

    Merge branch 'brandonmousseau:master' into master

[33mcommit dc36c2732e1e5d5d44b9414bde98eb56a276f3eb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 24 20:39:30 2021 +0200

    red outline for bow when out of stamina + load mats + shaders only once

[33mcommit 42adb53663bb1fa3128fb553799aedcef4fbcbfb[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jun 24 23:52:21 2021 +0700

    Added Configuration for Quickmenu rotation

[33mcommit 0f9374520b14399e7f56357258b2154a7b808ab2[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jun 24 23:15:46 2021 +0700

    Add both Camera and Hand version

[33mcommit c49b3cdda0e71c96724163ce85517614da0dea4b[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jun 24 22:07:26 2021 +0700

    Spear Throwing update
    
    - Only sheath spear if you ungrab the button (allows you to throw from back of your head)
    - Cancel throw if you don't move your hand much
    - Can throw slower
    - Less random throwing direction
    - Probably more accurate throwing
    - Fix Throwing on the movement direction while moving

[33mcommit 2057a0df586b477ecd30d760703af8d1e0b6c4a7[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jun 24 13:41:32 2021 +0700

    Some fix
    
    Remove unnecessary variable

[33mcommit 5dd5525b3b6629cdb121ea28b7246afd217e029c[m
Author: Aceship <terrenceleroy@yahoo.com>
Date:   Thu Jun 24 13:35:34 2021 +0700

    Update Quick Menu
    
    - Change Quickmenu to follow position of the player, but not the head rotation
    - Add Quickmenu angle to configuration

[33mcommit 7fe7817df2b69d8039a2fca7acf9c47117d1f31c[m[33m ([m[1;33mtag: v0.5.3[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Jun 23 21:49:37 2021 -0700

    Update version string to 0.5.3

[33mcommit 824ab437a8bd77bfe0962f5ab55c67377511eebb[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Jun 23 21:49:01 2021 -0700

    Update touch diagram

[33mcommit 718437134b20c82d308069faca70c0329706a954[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Jun 23 21:00:34 2021 -0700

    Move AltPieceRotation to Left Grab
    
    This used to be on the ToggleMap, which was originally the Touch's Y
    button, but the ToggleMap button was moved to the left Joystick button
    click. This ended up being awkward to use for piece rotation. This
    change moves the input to the left controller grab.
    
    fixes #57

[33mcommit e4b90eb16802b89d5d545c56599bd8169c6b9726[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Jun 23 20:30:51 2021 -0700

    Fix quick select when hammer equipped
    
    This fixes (albeit in a hacky way) the problem where when the Hammer
    equips there is a conflict with the Right Click binding and the
    QuickSelect binding. Now when in place mode, QuickSelect will also be
    activated by the right click input, so the QuickSelect menu still works
    and the weapon can be changed normally without needing to unequip the
    hammer in another way first.
    
    fixes #54

[33mcommit 935a0030805026f4ae7516f8c5942f47728a2b28[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Jun 23 18:31:03 2021 -0700

    Fix boss summoning
    
    The "fromInventoryGui" on the UseItem calls needs to be false to
    simulate using the numbered key button to use an item on the sacrifice
    altar.
    
    fixes #61

[33mcommit aa75224db4c4d4d2f3a6ca8fd3f3df3ecf75cbeb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 24 01:51:33 2021 +0200

    Fix helmet issue

[33mcommit 1f7901096abd27985bfe51078bea9a0b16c3a606[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 23 23:51:15 2021 +0200

    little cleanup

[33mcommit 9636d7363989e72a84392106859c52651b3f36c0[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 23 23:28:37 2021 +0200

    fix proj

[33mcommit dbe8080ee37503155f8623eb89e64a691077e41a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 23 23:27:48 2021 +0200

    complete overhaul cooldown system + non-vr multiplayer stuff (not working yet)

[33mcommit 43edf3178c9497ff69fa2a166a01941d0a2fec57[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 23 19:11:31 2021 +0200

    cooldown configs

[33mcommit 1ddf8a530e0a0f3f22a29c2a62fda694e2a39f0a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Tue Jun 22 01:27:02 2021 +0200

    improve weapon combat

[33mcommit d7e22a28862e8ef4461b8fe427948235ce42a911[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 22:24:18 2021 +0200

    ignore ui panel for collision

[33mcommit 2b2586478f769cea9675ffd6a1c263f6d53c0bf3[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 22:12:29 2021 +0200

    ignore water and watervolume layers for hit collision + mark broken weapons red in quickswitch

[33mcommit 44bfbee6ace93894d3631cfddc95c94e1ed1b588[m
Merge: 6654ba7 f8b9ead
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 20:35:41 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit 6654ba7c9fcd4cb1d7ac4e1e9a7a8900791e25c1[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 20:34:44 2021 +0200

    fix player vanishing at walls

[33mcommit f8b9ead31ee461c8260194dd3a2e232144f35aea[m[33m ([m[1;33mtag: v0.5.2[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jun 20 20:49:21 2021 -0700

    Update controller diagrams

[33mcommit c7a57baa3b824d2e558fbe0dc4e44c65c934e1f4[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jun 20 20:35:16 2021 -0700

    Enable inventory splitting
    
    Adds a new input to Laser Pointers to allow the player to modify the
    click by holding down the alternate trigger. This is used to replace the
    left shift modifier when clicking on inventory items, which enables
    splitting of inventory stacks.

[33mcommit e0d6401de0a51e8564625affb6168bcb288d3844[m
Merge: 504631c c954ee5
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jun 20 17:10:10 2021 -0700

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit c954ee58b5f1d6a4241aaa4f4e73648189948a8d[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 02:10:03 2021 +0200

    fix project file

[33mcommit 237d76b001de23431357d08246f06e43c7ce8217[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 02:08:30 2021 +0200

    side move threshold for run

[33mcommit 504631cd83f739f0985be200de240b02cff107d0[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Jun 20 17:04:56 2021 -0700

    Touch controller diagram update

[33mcommit 35f79ef07c2d61deb578300c0f5401d283e8e12b[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Jun 21 01:51:59 2021 +0200

    put left handed items to left quick actions

[33mcommit 3364084db2b8100e950515293ee9b9cc52b74d91[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 22:20:03 2021 +0200

    Torch Attack works now (both hands)

[33mcommit 60f690bd6d066ddbdaada71ef72276c58c74d58c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 21:00:20 2021 +0200

    add collider for axe iron

[33mcommit 001aa96a1e0fde83318fbbc90b1fca93c5e25930[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 20:32:49 2021 +0200

    Fix torch fire left hand

[33mcommit 2b4c16f42d39434cae71bad5459c6ce635579b4d[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 18:51:01 2021 +0200

    improve quickswitch/quickactions + add holographic controller bindings

[33mcommit 327bcc5f5a7c02387d9b5ea9ee3aae0fe0619aca[m
Merge: bd498e5 a54310a
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 01:26:15 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit bd498e5ae3e303c3e0d714b70e5b46de13e0c633[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 20 01:24:11 2021 +0200

    Fix Vive Controller, tweak weapon rotation

[33mcommit a54310adf31aa6ec13fbe205298d7cf114b09121[m[33m ([m[1;33mtag: v0.5.1[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 17:17:59 2021 -0700

    Update Touch bindings

[33mcommit 6dbda1710404effb0b6908c99ee6d7d906aa8ca0[m
Merge: b136794 c698a06
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jun 19 01:57:14 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit b13679437275e2747bec0c72925194384fbefaef[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jun 19 01:56:59 2021 +0200

    oculus bindings update

[33mcommit c698a06bc1dc2b7293e6ace25bd2ba3ca3a78eed[m[33m ([m[1;33mtag: v0.5.0[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 16:25:44 2021 -0700

    Add vive diagrams

[33mcommit b3237e1fa04b77a0f8ac099ee35915a97ca27eec[m
Merge: e5a7c48 b2b7e75
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jun 19 01:12:07 2021 +0200

    Merge branch 'master' of https://github.com/brandonmousseau/vhvr-mod

[33mcommit e5a7c48e86e6b3cf510bdedb9368ba4368b985d8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jun 19 01:10:48 2021 +0200

    vive controller bindings

[33mcommit b2b7e75565c018aeae0b2d7bb0b83523b57ba2c1[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 15:14:38 2021 -0700

    Set UseVrControls default to true

[33mcommit 7efa650e6c544bd24becfdd27e9b72f7c26c9d93[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 15:08:44 2021 -0700

    Increment version to 0.5.0

[33mcommit 4e3f5a2a2704c04157314876f2c32eff06333336[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 15:07:04 2021 -0700

    Add option to unlock desktop cursor

[33mcommit 7a63b79c57e6f9b879a3d530d95116444ade046d[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 18 15:00:09 2021 -0700

    Update control diagrams

[33mcommit 1cdb482b26ace4fe41c4da8d3e46610da79fa6da[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 17 22:46:52 2021 +0200

    remove keyboard/mouse hints

[33mcommit 48427a2bba589b9cace2d8eafdbc87133d03c3e8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 17 21:57:24 2021 +0200

    revert head scale stuff for non-vrcontrolls

[33mcommit 5f6f454a8cb35cac0426f51965294c8d16618006[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 17 01:40:41 2021 +0200

    make hair+beard+helmet invisible but show shadow, adjust camera pos, tiny fixes

[33mcommit d42d36221fc9963ad78a60e85e5c02af1578419c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 13 21:55:29 2021 +0200

    fix

[33mcommit d4b04f86ff6d49207b21d3958f39d2bec4b6908c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 13 19:24:38 2021 +0200

    tiny arrow fix

[33mcommit d62c0d10c6e9db4e6a4bacc85d78e2690f944ab1[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 13 19:11:26 2021 +0200

    Fix missing sit png

[33mcommit ff033f563fe8984a59fcaf2682c305285e45bdf7[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 13 18:22:40 2021 +0200

    add sitting to quick actions

[33mcommit 58ae690150c3b5554dab80b77a765119b2d9244e[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Jun 13 00:48:54 2021 +0200

    bow rotation and bow string for multiplayer

[33mcommit 1c7b844e21ea0c67ec4e2b8aad5ea6805b4b12ba[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 11 20:54:24 2021 -0700

    Add controls diagrams
    
    Add in some diagrams, including GIMP versions, of controls for Index and
    Touch.

[33mcommit bf4af97d4a535a9190badec038960ac7e9d041d4[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 11 20:05:18 2021 -0700

    Dont require hands to be active for VRControls
    
    Previously this check was enforcing the the hands are active in order to
    activate the VR control action sets. This just resulted in controls
    becoming inactive whenever the controller lost tracking, which isn't
    helpful at all.
    
    Now as long as the VR Controls option is enabled, the action sets can be
    enabled even if the controller loses tracking. That way the button
    inputs still work if the player loses controller tracking temporarily.

[33mcommit f5bfbcde565a7c3c53a9df5c3134f546d85bc941[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Jun 11 19:58:47 2021 -0700

    Add camera height adjustment when player is crouching or sitting

[33mcommit c2db02ef3e51bf17b4d0dd46f713a7e20c80233d[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 9 21:26:01 2021 +0200

    tiny bow tweaks + add menu button for oculus bindings

[33mcommit bf8cb321a0c0737f61f0b4853e1ee4d7cd23dccf[m
Merge: 0796fa4 ea7727f
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Tue Jun 8 10:45:20 2021 -0700

    Merge pull request #41 from brandonmousseau/vr_body_prototype
    
    Vr body prototype

[33mcommit ea7727f2496a29018d75c7964fc5d995b2b10903[m[33m ([m[1;31morigin/vr_body_prototype[m[33m)[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Jun 5 16:48:01 2021 +0200

    fix crouching

[33mcommit 978afcaaf3fbf5e9a47248076ae7641569dd1ce9[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 3 16:21:48 2021 +0200

    fix

[33mcommit 0bf520be1181dd8d0c2d4697403aa37c680142de[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Jun 3 02:32:43 2021 +0200

    improve finger rotations

[33mcommit 767d2b0e36f47acb1b3eb7506ec045c9a66179eb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 2 22:16:52 2021 +0200

    little improvement in turned off VRIK, lets simpley not send vr_data

[33mcommit 0fb655d992d63b71f32a2b1a0cae32170797b6eb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 2 01:23:08 2021 +0200

    remove log

[33mcommit 1ea709a5d2e8aea32b193887044050f5e349f1eb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 2 01:15:57 2021 +0200

    small refactor

[33mcommit 646e341b6fd4a16a303cc02b640dc50a4cd87d87[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed Jun 2 00:45:51 2021 +0200

    fix non-gpower - tweaked bow prediction line, fix of arrow, remove arrow trail, QuickAction tweaks

[33mcommit b08971ba290d895f02991f6229d7b5fe8552ba82[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon May 31 19:01:13 2021 -0700

    Update index bindings with quick actions

[33mcommit 97c9558468ddc7f32d866570cf83c01f398c9ad8[m
Merge: 3f07ffe fe239e4
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Tue Jun 1 02:48:16 2021 +0200

    Merge branch 'vr_body_prototype' of https://github.com/brandonmousseau/vhvr-mod into vr_body_prototype

[33mcommit 3f07ffe73bae4de9a5f0f97809455859287ecbea[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Tue Jun 1 02:47:31 2021 +0200

    implement Quick Actions

[33mcommit fe239e4e394a193f8a141a49caae42da70fe67d9[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon May 31 15:37:30 2021 -0700

    Send UseVrControls option in VRSync
    
    Sending this option to other clients will allow them to respond
    appropriately and not add VRIK to other Player when they join if the
    other player is not playing with VR controls. This will enable
    non-motion controller players and motion controller players to play on
    the same server. Non-motion controller players will be able to see the
    VR animations of other players and non-motion controller players'
    animations will normal to everyone else.

[33mcommit 7ce9dabee83f17d7e1d6a8c3350b16175b701636[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon May 31 14:58:41 2021 -0700

    Update player transform prediction
    
    Update to more closely match ZSyncTransform algorithm

[33mcommit 38486a95eb4f7d6ac21a4d031bf53da2e49f6bad[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 22:42:58 2021 +0200

    fix

[33mcommit dfc1d4c894823ef945cb62a00c38515032c29107[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 22:38:03 2021 +0200

    fix

[33mcommit 4cfe834f2dde7cbc48c18501d122cea68024b2d8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 22:30:12 2021 +0200

    fix

[33mcommit c4587fe54caa5521331a2fab331307c70818d420[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 21:08:01 2021 +0200

    finger fixes

[33mcommit 1790d795ad97e1a104fccd15d7769b1354aac063[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 20:27:18 2021 +0200

    fix

[33mcommit 44b59f894087529f5f4dc2354c7a703ee6621d1a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 20:22:02 2021 +0200

    fix

[33mcommit 990da28ac9302411184cad44d4374efe9a9db310[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 20:14:03 2021 +0200

    Finger Sync

[33mcommit c736fdf8bb204420396cc196cb3c7c3687540173[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 19:33:54 2021 +0200

    fix missing localPositions

[33mcommit e466fe85b3cedb5572cca75ebbd4c0473f6d111a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 19:30:36 2021 +0200

    try localposition/localrotation

[33mcommit 53f747b227361f4da526d5eecd52ccf35aa55bfc[m
Merge: c969eb9 cf0a9f1
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 19:10:28 2021 +0200

    Merge branch 'predict_test' into vr_body_prototype

[33mcommit cf0a9f16bedffef86e1371f0c4f9156b41de7a11[m[33m ([m[1;31morigin/predict_test[m[33m)[m
Merge: 5517768 d770660
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 18:34:24 2021 +0200

    Merge branch 'predict_test' of https://github.com/brandonmousseau/vhvr-mod into predict_test

[33mcommit 551776839fab02367e903438eb8d14b16c4f738a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 18:33:34 2021 +0200

    maybe fix multiplayer collision

[33mcommit c969eb9b7f1c72b81eb4dfea796eb4680fe5439d[m
Merge: 6f07c77 d770660
Author: brandonmousseau <52470509+brandonmousseau@users.noreply.github.com>
Date:   Mon May 31 08:15:53 2021 -0700

    Merge pull request #39 from brandonmousseau/predict_test
    
    Predict test

[33mcommit d770660174dd62931aa07954f317e977ab83fc56[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon May 31 08:11:58 2021 -0700

    Fix some null reference errors

[33mcommit a9996b56e8cd8a3fc6e07091cdde563d24ad7f72[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 31 17:07:44 2021 +0200

    fix arrow destroy

[33mcommit 0516d4c73df38a3e7104dd9c02bbd769b18c933e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 30 17:00:16 2021 -0700

    Update owner check

[33mcommit 92307bd3f5e09e197dbd45b73d00ed6428719fbe[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 30 16:30:41 2021 -0700

    Attempt transform predict algorithm
    
    This is an attempt to mimic the ZSyncTransform calculations that will
    predict a transform position in space based on its current velocity so
    that we can generate smooth movement with our VRIK on remote players.

[33mcommit 05ec644bf839b8bf5c5e5345223566551f812fc5[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 30 15:38:01 2021 +0200

    little optimization

[33mcommit 39e30328de7343940f3df162e9148cac0a27aeed[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 30 15:31:35 2021 +0200

    another aproach

[33mcommit 6f07c775cf651f67ccc1828a7f032cb0e3dfe391[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat May 29 22:51:44 2021 -0700

    Guard motion control patches with UseVrControls
    
    Only run these patches when the player has opted to UseVrControls

[33mcommit 227298683b18aaebc706b4e37c359faa7164827c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat May 29 17:38:22 2021 -0700

    Remove UseVRIK and EnableHands config options
    
    This change removes the UseVRIK and EnableHands config option in favor
    of using UseVRControls for all of these settings as it no longer makes
    sense to have these be independent configurations.

[33mcommit 2bba144b009001d3006cb54fe0f1b75b41436197[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat May 29 17:10:09 2021 -0700

    Enable indepenent Use input for left hand
    
    This change adds a new binding that can be used to independently input a
    Use for the left hand and be used to interact with objects in the world.

[33mcommit 172146df19b51a9e893ddbaec0b933e14b89a2fb[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat May 29 16:25:11 2021 -0700

    Add tracking for left hand crosshair and hovertext
    
    This change adds a clone of the hover text that will be tracked with the
    left controller. The controls do not yet work or interact with this
    however and it is just visual with this change.

[33mcommit d890c75a32d655e7b04bc34f1c33478f6bedfbea[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 23:51:55 2021 +0200

    fix menu error

[33mcommit 91cd878c9d4d533e353cadec6254d353ec8afcd8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 23:45:54 2021 +0200

    try

[33mcommit 687f9f4db7c96f798b452e6046615382149ad013[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 23:40:59 2021 +0200

    fix

[33mcommit 22b7c52ac7536c40ac38d944fcf3a85d210ae4a5[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 23:22:01 2021 +0200

    neeext try

[33mcommit b905ffe792d3638a9b4eb56fb12e6b86b91d0dbd[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 22:28:53 2021 +0200

    blub

[33mcommit 84e67abf427817de05067605471c98e70ebb01ad[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 22:25:55 2021 +0200

    bla

[33mcommit 4e8d30564bec78e2757acc810d5f935643552523[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 22:25:08 2021 +0200

    next try

[33mcommit 3e8114cad615cff6d43ac00b428de6c79861ecf2[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 19:23:08 2021 +0200

    trying things

[33mcommit e1c5d621938f11c64f9bebd66bf10f3183e72859[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 18:50:13 2021 +0200

    fix

[33mcommit 6ba6a14b86607381be880fab514be503d3aa9011[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 18:34:22 2021 +0200

    wip

[33mcommit 6e1b5e6baea72be41402e8a816218ebd86c5b866[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 18:05:42 2021 +0200

    wip

[33mcommit 2f14a5c675b339da7ae64c4e6ecb7542031f5c26[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 17:55:38 2021 +0200

    wip

[33mcommit 8eefba3695522cfce8e6b539fa94c88959988bd8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 17:41:07 2021 +0200

    wip

[33mcommit 068a936b67bca4144dfdb46b441d79626f37e4fd[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 15:58:42 2021 +0200

    multiplayer test

[33mcommit c6892624b02fb10008a2ef8aa8b83bcdf6b16c9d[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 02:59:28 2021 +0200

    refactor

[33mcommit c7c0aee781b960cdd8c2f28c9e9fe0759ee42531[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 00:59:43 2021 +0200

    fix

[33mcommit 856c8b8cccda30c29c0c72d4e3361983748a3294[m
Merge: 0e8b0ec ec960fb
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 00:40:15 2021 +0200

    Merge branch 'vr_body_prototype' of https://github.com/brandonmousseau/vhvr-mod into vr_body_prototype

[33mcommit 0e8b0eca94774b80578a8c4f33632befc0c3014a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 29 00:40:06 2021 +0200

    Fist Attacking

[33mcommit ec960fb8177a1d9d22e90013743b496f32a9dff7[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri May 28 11:11:46 2021 -0700

    Make interaction based on right hand pointer

[33mcommit f2fc91e6bd69205e78d1a417dbbdd45fa2356f8a[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri May 28 01:22:23 2021 +0200

    refactor

[33mcommit 95d75e8ec4d816027f6a1a16d4d7291fc6bdf9f8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri May 28 01:01:44 2021 +0200

    small refactoring

[33mcommit 983fd3749d9f7c046ec0b651509fe98ed17a8625[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 27 23:14:15 2021 +0200

    implement Hand Gestures

[33mcommit e6684f17507479b89b53b9f0a83aaae8df8b080c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 24 22:38:39 2021 +0200

    more haptic feedback + fix out of ammo exception

[33mcommit b318c9e53f6149223c5db6bce29d9223b9ac3843[m
Merge: 9b32418 cc45c96
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 24 21:04:09 2021 +0200

    Merge branch 'vr_body_prototype' of https://github.com/brandonmousseau/vhvr-mod into vr_body_prototype

[33mcommit cc45c96a9f39920f411895113ccda48ea565543a[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon May 24 09:45:57 2021 -0700

    Reverse alt piece rotation controls

[33mcommit 9b3241890343bffb06cb6472fe1cb14a7535f22c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 24 15:08:16 2021 +0200

    make cooldown visuals look at camera + fix fishing throw angle + add vibration when fish at fishingfloat

[33mcommit bdf42ee9122864e1e3aec7479e8918ba6c011673[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 24 11:34:58 2021 +0200

    fix shield + fix torch

[33mcommit d27e0c4894156cff9a5e25a2932a82acf7a0a382[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 23 13:35:05 2021 -0700

    Update bindings

[33mcommit 7a11f1dc98625f617b3dc317829f3996ab773e58[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 23 13:34:49 2021 -0700

    Fix build controls, including alt piece rotation for touch

[33mcommit 0696f5b267b3d0aa81db0d4c266d4f86d09094b1[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 23 13:00:55 2021 -0700

    Update Index Right Click to B button

[33mcommit 0d659570b1fb14d3516ff2cedc227c38c0277d73[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun May 23 09:09:26 2021 -0700

    Assign quick switch to left trigger for Knuckles

[33mcommit 7026f3abaaec2f37aef4b48acb0ffc2b8aacbeea[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 23 13:42:20 2021 +0200

    fix vrik elbows for latest hand rotation fix

[33mcommit 63a1cf1695451e5f69f07d5ed469da5aac26d1fb[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 23 03:31:39 2021 +0200

    fixes..

[33mcommit 71689449caa541959ed7668cd0db5cc10eee0f72[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 23 02:59:40 2021 +0200

    implement Shield Block and Cooldowns + Tweaking Attack stuff

[33mcommit 9fb4ee605d47793e0d99366934897af87c8b28aa[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu May 20 21:56:48 2021 -0700

    Enable Quick Switch trigger to be from either hand

[33mcommit d973f4165f9734bffff362abf2e2c6c48ed6d8a2[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu May 20 21:56:17 2021 -0700

    Move QuickSwitch UI to WorldSpace UI Layer

[33mcommit f2db8d60a0e11772416ac9cf90703a8f3758d9ef[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu May 20 21:30:25 2021 -0700

    Make Arrow prediction line user option
    
    Add ability to disable the arrow prediction line via configuration file.

[33mcommit 5fd7f977abba38ef09d78595f71e093679cc23f8[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu May 20 21:21:13 2021 -0700

    Fix mouse input bindings for index

[33mcommit 552eced28f90f6ce2d6735555cb6bbf830809177[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri May 21 00:23:42 2021 +0200

    SpearChitin special Spear type, and some small fixes

[33mcommit d829a083e6941ad00e0d39515191b7f8e9a3ec20[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 20 22:55:51 2021 +0200

    Implemented Spear Throw!

[33mcommit 41722ca90cb12d63cf01f1c57bf5feaca927d04d[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 20 20:16:26 2021 +0200

    Project Refactoring

[33mcommit 8eba5f1b6a66832d015684f2ca4a1a0f1d5d99b6[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed May 19 22:02:44 2021 -0700

    Delete hotbar controls in favor of quick switch

[33mcommit d348ca3a8614ffc8066ab22e92e6b99b9087f9bc[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed May 19 21:54:51 2021 -0700

    Delete unused reference

[33mcommit 89dd9fbf9cf9bc915d4d09e9642355a2cfbb9a73[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed May 19 21:54:35 2021 -0700

    Update knuckle bindings

[33mcommit 3f828179ddc8efe682933b470709ac09bf2ab97e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed May 19 21:54:14 2021 -0700

    Delete stray file

[33mcommit 6c249b72efac6ed28c38b0082b8c121e1c70ef3c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed May 19 21:32:56 2021 -0700

    Use CommonDir variable for references
    
    Update csproj file with a CommonDir variable to make it easy for
    developers with different directory structures to use the same post
    build script and reference assemblies without needing to have a lot of
    custom edited locations. Now just point the CommonDir to the game's
    installation directory.

[33mcommit 5dd0eca52067a143bcdbdb00369082b9950918d3[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed May 19 23:12:46 2021 +0200

    implemented Fishing ! removed bow inaccuracy and fixed some small bugs

[33mcommit 2189c0256c4f96b7f84270009d03b6ca0186de35[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 13 16:18:52 2021 +0200

    Bow aprovements: fix canceling shoot, add prediction line renderer, remove pull animation (it was fucking up the shoulder position)

[33mcommit 855e43b9688e6591b080371c39ea62f22fa1068e[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed May 12 18:06:14 2021 +0200

    fix null reference exceptions

[33mcommit f6084c7c22cc21699dc45dad70a7f771a8751263[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed May 12 00:55:52 2021 +0200

    fix trigger for refresh quickswitch

[33mcommit e7ebddabdab84f974fe514949802a89ecc6b26aa[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed May 12 00:44:19 2021 +0200

    QuickSwitch implemented + run, crouch bindings + cleanups

[33mcommit b541cc53ca80349a48907cd1bfb7ca488c2c6aa0[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 8 00:51:50 2021 +0200

    added rest of weapon colliders

[33mcommit 0bad5b80eb344750fca51e7ba812f206befe32f7[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri May 7 23:45:10 2021 +0200

    BIIIIIG COMMIT, Bow is finished, button bindings are refactored and Weapon Hiding over shoulder working

[33mcommit 7f04631b16e477f8bb15836984f4b7183284f121[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 6 17:15:11 2021 +0200

    Fix Bow Mesh and optimize code

[33mcommit 2fb409ec3a490c2dcefd3dcd93110225c4197779[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu May 6 02:07:50 2021 +0200

    Bow itself kinda works, Arrows still missing tho

[33mcommit a1b6a3058d8316dff0407335ab3e68919b168c97[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Wed May 5 23:58:58 2021 +0200

    bow work in progress

[33mcommit d32c4848f0b3065c79628ad7caed6563e77206dc[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 3 23:11:19 2021 +0200

    optimization: we actually had 2 instances of VRIK

[33mcommit 9e8e7addf148b352d0ebf4bfc9acd1770072cf44[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 3 22:50:57 2021 +0200

    finally fixed ugly head rotation bug

[33mcommit 5284b2a6489721059c4b8864b86a87dbbb18b6a9[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 3 20:18:36 2021 +0200

    fix

[33mcommit 65a2803b8548dd43fa99311324b3ea2ff0ce9365[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 3 20:16:56 2021 +0200

    remove bowstring v2, actually this is smarter ...

[33mcommit 06b5da6e09edc965b90b4086304dbfd366f164f6[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon May 3 20:06:17 2021 +0200

    remove bow string

[33mcommit d90dcb9abe5a5b7d4baea55b0fac45b582d07a6b[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 2 17:33:05 2021 +0200

    Remove Collider MeshRenderer for Release Build

[33mcommit 0ada784d5eb7ca69b3ed978383ed6dff34dfe0a2[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 2 17:14:46 2021 +0200

    Haptic Feedback for hit + momentum fix on walking

[33mcommit d66198aada4d4a9f3631163da1d8a97b35884a6e[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 2 13:51:27 2021 +0200

    TONS OF FIXES

[33mcommit 50df28c400e07da2150c10396dc76b1fe35c0747[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun May 2 00:11:01 2021 +0200

    add stamina usage

[33mcommit dca6ac9ca1dc3cca73b5811cb1335a4697d3e1e5[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 1 23:40:27 2021 +0200

    formatting

[33mcommit 1a575a16f5670f4ed97cd110a5ba7143b6564f1c[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 1 22:54:41 2021 +0200

    little fix

[33mcommit 1b6344b40ca9fd8c0f95d43f09c18a9bc30ca614[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat May 1 19:31:15 2021 +0200

    lumberjacking works kinda

[33mcommit fbdb5cc08288140c99e4fee0f8d68f5aaee3b489[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Apr 30 23:23:11 2021 +0200

    COLLISION DETECTION first progress

[33mcommit 5ec9f0d84f5ee899cf58ff95a3807bac46ac5158[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Apr 26 21:37:50 2021 +0200

    Helper class

[33mcommit 1006c71f036c830fc526a66ee15b49ce30774dc8[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Mon Apr 26 20:51:43 2021 +0200

    oculus touch bindings

[33mcommit 02092c8cd9ea1d8ca6400e9f1e306357136ce3ef[m
Merge: f123441 0796fa4
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 25 12:51:50 2021 -0700

    Merge branch 'master' into vr_body_prototype

[33mcommit 0796fa440992766bd01795fb8cd6db2d8ada6d75[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 25 12:51:25 2021 -0700

    Fix laser pointer rotation and position

[33mcommit f123441ab4c84a5c01a9ddccda3c073e314fd4c3[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 25 11:09:15 2021 -0700

    Rename method to be more specific

[33mcommit 86328dd3ab8b655176f0805aff9f5ddde691dff7[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 25 11:07:38 2021 -0700

    Disable vrik during wakeup animation
    
    Disable VRIK when the player is in the wakeup animation so that there
    isn't super goofy stuff happening with the legs.

[33mcommit cdd67eb9cd0e94dffadb87ebb764eff1151d6135[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 25 10:38:13 2021 -0700

    Add VRIK config option + disable VRIK in TP
    
    This adds a new config option to disable VRIK tracking as well as
    disables VRIK when not in first person mode. It also sets the SteamVR
    hand visibility to true only when VRIK is not enabled.

[33mcommit 16a3cdd2f88fe33627fb4d364fcc4ab07b7f02ac[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sun Apr 25 19:13:57 2021 +0200

    fix hand rotation

[33mcommit 5a5c3e386802e0fa6a34aac209b856721da01362[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Sat Apr 24 02:09:03 2021 +0200

    tweak head camera

[33mcommit 49ee9c238ad7033396fb8bae2cf43f7435d1bfeb[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 23 16:57:21 2021 -0700

    Hide SteamVR hands after attaching to player character

[33mcommit bd97f85e64bbbee60748ad575b959390668ca81a[m
Merge: 81fedff 48a359f
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 23 15:36:49 2021 -0700

    Merge branch 'master' into vr_body_prototype

[33mcommit 48a359fd4b220b030506efa2cca507732518df21[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 22 11:24:39 2021 -0700

    Update enemy hud patch to match original logic more closely

[33mcommit 1a2c2e9167eb8e903fcda32dda77f0b98c1bed60[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 22 11:23:57 2021 -0700

    Only enable lasers if using VR controls

[33mcommit 25572d1555e67c4d603ea2979909b8b26d287265[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 22 11:23:28 2021 -0700

    Only use VR controls for placement if using VRControls

[33mcommit 223652a3c9fe22e24611335b2b8a3f1b9e8c17a6[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 22 11:22:56 2021 -0700

    Use UseVrControls option

[33mcommit 28f49482c4aade15327180e8df267010552f402c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 22 11:22:33 2021 -0700

    Add UseVrControls option

[33mcommit 8bf2eccf3dedef25c5d0a6e2731ea9afc42970fe[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 21 15:18:19 2021 -0700

    Don't reposition null or inactive enemyhud gui

[33mcommit 1e4f961509fa5d1bf754e34fc59533b3dc599e10[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 18 16:57:47 2021 -0700

    Enable index controllers
    
    This change maps required inputs for the Valve Index controllers into
    the game.

[33mcommit 81fedff83d730f35bc835e5c1c50fa1d521f1f70[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 23 14:40:07 2021 -0700

    Set legs to null and copy target transforms

[33mcommit 627a7ee764e8e48e16af3ed316e743fb44ca7600[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 23 14:37:35 2021 -0700

    Attach VRIK to PlayerModel
    
    This change moves the VR_IK_Creator into the FinalIK directory so that
    it is built with the dll file. It also replaces the bone initialization
    with the auto-initializer and sets all the leg bones to null.
    
    The VR_IK_Creator is assigned to the game's player character and assigns
    camera and hand targets.

[33mcommit a675ca7e945f8c1587daf3b49bbeae4dc5ed221c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 23 06:05:57 2021 -0700

    Create assembly definition for FinalIK
    
    This change adds assembly definitions for Root Motion's FinalIK library
    so that we can generate DLL files to be copied and used in the mod
    project.

[33mcommit 77ad9434af75bd6c4993c9ae91fe4bbbdfbf3f05[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Apr 23 14:24:24 2021 +0200

    fix, better use custom initialize instead of awake

[33mcommit fc7173766712d61df56b27769edc63d113f20c96[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Apr 23 12:57:33 2021 +0200

    VR_IK_Creator Script to enable IK at runtime for ingame model

[33mcommit d68a2a122edf7920a5f7d60b996bf5674e5b1be9[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Apr 23 01:51:06 2021 +0200

    remove dependencies

[33mcommit 742ca84a501a487a7ee46b636c36455d23a063a6[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Fri Apr 23 01:40:36 2021 +0200

    added no-leg variant

[33mcommit fe06de5dc077de76a9958ded81c56a0bd7375e7b[m
Author: Andi1986 <andreas.hof@gmx.de>
Date:   Thu Apr 22 23:56:30 2021 +0200

    VR Body Prototype - intial commit

[33mcommit 20e8f0f0e813133a5af04988d9092e9c0a347396[m[33m ([m[1;33mtag: v0.2.0[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 16 17:40:51 2021 -0700

    Fix config option name

[33mcommit ee49c768c113dd5d8f0200f9553cc4810b2a3322[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 16 16:26:00 2021 -0700

    Minimap player icon matches body rotation
    
    Instead of using the default behavior where the minimap icon rotation
    matches the game camera rotation, since we are in first person and look
    direction might diverge from walk direction, we should use the direction
    the body is facing as the rotation, as this will now always match
    forward walk direction.

[33mcommit d202936dd5dca982c5a69ab0aca2f9e43ea9c70e[m[33m ([m[1;31morigin/LookLocomotion[m[33m)[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 16 15:30:32 2021 -0700

    Keep player body always rotated with look yaw

[33mcommit 4d47902c160d3ce9a8ddd3d736ba6b0d87225873[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 16 14:51:19 2021 -0700

    Prevent UIPanel from interacting with physics engine

[33mcommit ee55d85bf7d0cd3cac45427f9d8b01bc3213ece7[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 15 21:42:52 2021 -0700

    Updates to looklocomotion

[33mcommit 857996746110cbcbc2d734c94e08e105904db88b[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 15 08:21:27 2021 -0700

    Some small refactoring & bugfixes

[33mcommit dbc05992a7b826d19082ce41819697be432c247e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 14 22:15:15 2021 -0700

    Enabled look based locomotion
    
    This change adds look based locomotion along with a system whereby the
    GUI will rotate only when the head turns outside of some pre-defined
    angle to allow the player to move their head around while checking the
    GUI without having it constanly move away from where they want to look.

[33mcommit 34ce0b876c8103bb2c5bd0aafbb78b2f439e91d1[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 13 19:50:29 2021 -0700

    Small changes to prevent null reference exception

[33mcommit 2ade68888dc287030aa1b11c4f2a7fb80d23daee[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 13 07:30:44 2021 -0700

    Use layer mask for SteamVR_LaserPointer
    
    The laser pointer originally uses hte default raycast layer mask. This
    update adds the ability to set it as an option and changes it to only
    interact with the UI panel.

[33mcommit 66984399b9724855fd8dedf05b7e98aff3806995[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 13 06:54:33 2021 -0700

    Add EnemyHuds configuration options
    
    Added the ability to disable enemy huds if desired as well as scale
    their size up or down.

[33mcommit cac3a076cf4b3a01645076df0661edc476cb9245[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 13 06:40:45 2021 -0700

    Update version string to 0.2.0

[33mcommit b31034cf7378b902c531ddb4c0e763b9c3703ec6[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 21:41:22 2021 -0700

    Update version string

[33mcommit a1687d12fcb6908e31c40d813fefa3177154e442[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 21:38:57 2021 -0700

    Ensure original enemy huds always inactive
    
    Update the EnemyHuds transpiler to always set the second
    SetActive(true) call to false instead. This doesn't impact the boss huds
    which will be displayed on GUI panel.

[33mcommit cbdcb39b1722ae305642420aabe0732d56db5a2d[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 21:21:28 2021 -0700

    Move piece health bar to hovering piece
    
    In the base game, the health bar for the hovered piece in place mode is
    placed at the crosshair, which is disconnected in the VR mod from the
    placement ray vector. This moves the health bar to be positioned on top
    of the currently hovered object.

[33mcommit 72b9816b323f040d557a942308b5817854a932cc[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 20:46:52 2021 -0700

    Add missing properlty to world space UI camera.

[33mcommit 3f3f18285de6f1cea7fed6fb48e34e3c59b557ef[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 20:44:10 2021 -0700

    Add placement mode visual cue
    
    In the base game, the placement cursor is centered on the crosshair, so
    it is easy to know where the placement cursor is. Since I separated the
    cursor from the crosshair, it can be difficult. This change adds a
    visual cursor in the form of the Hammer icon to indicate where the
    placement cursor is whenever there is not an active placement ghost.
    
    fixes #26

[33mcommit eb4cb5f2e0db575866b32a4ebb6306518fa4dbf9[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 12 17:38:53 2021 -0700

    Fix crosshair and layer related things
    
    This commit bundles a few updates. First the crosshair position
    management was fixed. It was pushing all the elements in the crosshair
    right to the center of the crosshair canvas. Now rather than doing that
    I'm just properly parenting things so everything is moved automatically
    but in the correct location on the canvas. This fixes the position of
    things like the stealth bar and piece health bar.
    
    I also refactored the layer management to a central class since it was
    starting to become difficult to keep track of. I combined the crosshair
    and enemy hud layers into a central "world space UI" layer and camera
    that can be used going forward for any UI that needs to be rendered in
    world space outside of the normal UI canvas.

[33mcommit f47456ad3c314819c3f54f29f352882ef7877e9e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 11 21:58:34 2021 -0700

    Fix crosshair raycast
    
    Fixed bugs related to the crosshair camera raycast not being positioned
    correctly resulting in bad distance calculation for the crosshair.
    Filtering out non-solid layers for the crosshair distance raycast.

[33mcommit df0ec45acd6c722d7a004de96a44f6cd752bb4dd[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 11 17:41:18 2021 -0700

    Fix rudder control UI
    
    This change moves the Rudder control UI to be positioned just below the
    ship wind indicator on the UI.
    
    fixes #24

[33mcommit 9b0e70bbd8d09662a4ff929fe0444140eb3f432c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 11 11:29:25 2021 -0700

    Update First Person camera positioning
    
    Previous to this change, the first person camera position was being tied
    to the player model position using the current HMD transform every
    cycle. This turned out to be a bad idea because it means if the player
    leans forward, the positioning of the camera is moved backwards to
    compensate and keep it on the player model, which creates the effect that
    the whole world is moving forwards with your head rather than you moving
    your head.
    
    Instead, now it will only account for the current HMD position on first
    startup or after HMD tracking is recentered. The offset calculated is
    saved and used as a constant offset until next recenter. This change
    also removes the usage of the head repositioning config options for
    first person modeas it no longer makes sense to use them given this new
    strategy. The player can still adjust them in game, but everytime the
    tracking is recentered this offset will be reset to zero.

[33mcommit a66edb8b8041bf1093beb772ca762f343df8ff5c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 10 23:13:31 2021 -0700

    Complete EnemyHuds impelmentation
    
    This change completes implementing EnemyHuds, so other characters health
    bars etc should be visible for a time after the player vision hovers
    over them. It is using a Transpiler to inject method calls to my own
    class that is being used to create on-demand world-space Canvases that
    will be located at the character with the Hud. The transpiler injects
    method calls to the UpdateHuds method to update my copy of the hud data
    to make sure it is getting updated appropriately.

[33mcommit 2648faa6fb4b1bc127cae1e3416b4a85e9ae1ff9[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 10 17:47:58 2021 -0700

    Add basic EnemyHud support
    
    This implements super basic functionality for displaying the EnemyHud
    GUI elements above the actual characters in world space. It is
    incomplete as there needs to be some functionality to update different
    values etc as defined by the EnemyHud class.

[33mcommit a22e3a8194145ad96d89bf3c228558b2f3192269[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 10 08:15:45 2021 -0700

    Fix Hover object raycast
    
    This change fixes the Raycast that is used to determine what is the
    current hover object or character. The original method uses the game
    camera as the origin and direction of the raycast. This patch swaps that
    with the VR camera.

[33mcommit 73a4457348c3e025bf8fa959a47b0bad26b7d32e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 10 07:25:18 2021 -0700

    Further refactor Raycast vector transpiler
    
    Refactor this transpiler more so it can be used in some additional
    contexts.

[33mcommit 0f0377e6f27c94d528eaf64e967388da7ad470ff[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 9 19:46:04 2021 -0700

    Rename VVRConfig to VHVRConfig

[33mcommit a219193d66b74cabde78bb87d79f2da4ed768523[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 9 08:51:24 2021 -0700

    Remove custom version string and use BepInEx versioning

[33mcommit 50560ce3a36b43d26e3622c52c63a058c9a15a0a[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 9 07:27:11 2021 -0700

    Fix cursor alignment
    
    Updated the cursor image position on the canvas so that the pointer part
    aligns with the simulated cursor position.

[33mcommit d31f7a4405cd4f2c3c5ccb0a2225f9aff8e01a54[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 22:55:30 2021 -0700

    Update version string

[33mcommit cbeed05e995ff0d357522bcec9e81982a3f4b66c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 22:41:17 2021 -0700

    Add ability to reposition third person camera.
    
    This change adds the ability to resposition the third person camera in
    the same way as the first person camera for players who wish to adjust
    this. The offset for each of the 3 third person zoom levels will be
    shared, but third and first person offsets are separate.

[33mcommit eb4bb321f09be135c2ec817994f7a128317ec27a[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 22:15:53 2021 -0700

    Added options to reconfigure VR specific keys
    
    Added new config file options to allow the player to pick which keys
    they want to use for repositioning the head as well as what key to use
    for recentering tracking if they want.
    
    resolves #12

[33mcommit 1be26d021c2a33e7956392feb344fc735ac63549[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 21:34:19 2021 -0700

    Add config options for UI panel size and position
    
    Added some additional options for the user to customize the size
    position of the GUI when not using the overlay.

[33mcommit b8c8e54647f8963cb682ca4956dfb27e12c8fd5a[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 21:16:24 2021 -0700

    Fix overlay gui positioning and add config options
    
    There was a bad offset causing the overlay gui to be slightly off
    center. That was fixed and several new config file options were added to
    allow a player to customize the size and position of the overlay.

[33mcommit f83466ba572fc26205e8948910b7709b4c147f53[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 20:22:29 2021 -0700

    Reorganize config entries
    
    Reorganized config file settings into better categories. Unfortunately
    will require overwrite for anyone who has an existing config file.

[33mcommit a8bbe335c6115be2bc32d640d1f3b326f7f0f89e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 18:04:20 2021 -0700

    Do not set VRPlayer as UIPanel parent
    
    The UI Panel uses a non-convex MeshCollider. After parenting the UI
    Panel to the VRPlayer, and subsequently parenting the VRPlayer to the
    game's Player model, an error is thrown related to using a non-convex
    mesh collider with RigidBodies, which is not allowed.
    
    This was causing the laser pointers to no longer get a raycast hit with
    the UI panel once attached to the Player.
    
    The position and rotation are now updated manually each frame.
    
    fixes #18

[33mcommit 48950d8cd537221ec04ce4084b0576df5ffd1f6b[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 17:27:17 2021 -0700

    Fix Set FOV Log Warning Spam
    
    fixes #11

[33mcommit 44e9d77b3abfbe87389072a0d187ae1587722cbf[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 8 17:25:23 2021 -0700

    Fix ShowStaticCrosshair option
    
    fixes #19

[33mcommit 01fe51de952c0e8c7fa25c7b010e15133a55ef5f[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 7 22:38:59 2021 -0700

    Add version log statement

[33mcommit 6829eaa8f70e25ffd0741213c0b5fbfcb178ce24[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 7 22:20:27 2021 -0700

    Center cursor when it becomes visible
    
    The cursor will now be centered whenever it becomes visible.
    
    resolve #17

[33mcommit e9658e6fcf0860adf9b4c95f0c0eed7e7483cab4[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 7 22:16:32 2021 -0700

    Move crosshair to unused layer

[33mcommit 69fc4f933d1bf3946a5c1425b5ea25af3b98843e[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 7 21:34:14 2021 -0700

    Account for depth in crosshair positioning
    
    This change causes the crosshair to be rendered at the depth that the
    object being aimed at exists as well as scales it appropriately to keep
    it at the same draw size on screen.
    
    This allows the crosshair to actually be effective in aiming with ranged
    weapons and prevents double images from forming since you will no longer
    have to focus on the crosshair and an object at a different depth
    simultaneously.
    
    resolves #15

[33mcommit 9cd5adba3431a7c864097a0a4753629b448ed457[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Wed Apr 7 20:43:22 2021 -0700

    Fix bug with broken crosshair after death
    
    It turns out simply moving the crosshair elements off of the man GUI
    canvas onto the separate canvas broke things after the player died. All
    of the crosshair elements are normally destroyed and rebuilt on death &
    respawn, but because I removed the elements from the canvas, they were
    not getting recreated correctly.
    
    Instead of moving the crosshairs off of the canvas, I am just cloning
    them, adding the clones to the separate canvas, and disabling the main
    canvas crosshair elements.
    
    This change also moves crosshair management into its own class as there
    will need to be more logic to deal with moving the crosshairs to the
    proper depth and scaling them.
    
    fixes #16

[33mcommit 1f221918ff6ece1b1ae05e83a1de4856285b2300[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 6 20:59:17 2021 -0700

    Add default on option to recenter on start
    
    To help avoid players starting the game facing backwards depending on
    their tracking, this change provides an option to just always recenter
    tracking on startup.

[33mcommit bd36992559688b91e7e9383d1cde0efab40dbba1[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 6 20:50:23 2021 -0700

    Add crosshair support
    
    This change adds support for the crosshairs. What you interact with and
    aiming is tied to the direction you are looking, so this change places
    all the relevant crosshair and interaction info into a canvas directly
    in front of you.
    
    This is not an ideal solution for the crosshair and it needs work. The
    text that is displayed for interacting with objects is alright, but the
    normal aiming reticle and the bow aimer, while in the center of your
    vision, are off when it comes to where you are actually aiming due to
    how the eyes are focusing on objects with stereoscopic displays.
    
    I'm including it in the next release since, while flawed, is an
    improvement over the current crosshair, which just sits in the middle of
    the GUI.
    
    This should be replaced with a reticlule that is displayed at the depth
    the object being aimed at is actually at. Eventually with motion
    controller support, the aiming reticle can be attached directly to the
    bow and aiming can happen more naturally.

[33mcommit 7f245bd116242e63eba969e6c8b0c5e9bcf879ac[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Tue Apr 6 10:08:26 2021 -0700

    Refactor recenter method
    
    Refactor this to allow for recentering for other scenarios where it
    makes sense.

[33mcommit a95d24e8c95775879d00cab4aa91d4066a631042[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 5 21:37:55 2021 -0700

    Dont pause or change render scale on dashboard
    
    This patch skips a SteamVR_Render function that is called when the
    dashboard gains or loses focus. The function is responsible for pausing
    the game when the dashboard is visible as well as setting render scale
    to 0.5. Pausing the game breaks the game completely, so when the game is
    unpaused, things do not get restored.
    
    Setting the renderscale to half doesn't break the game completely, but
    things still end up being blurry even after the render scale is
    restored, so something doesn't work right there.
    
    Skipping it doesn't cause any problems and fixes a major problem users
    have.
    
    fixes #4

[33mcommit b271477893b0a8e56ce84d23688694fc238f4a9f[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 5 17:44:38 2021 -0700

    Enable joystick use for placement
    
    Takes input from the Right joystick on a gamepad to provide vertical
    axis placement with the gamepad.

[33mcommit 69082e190681fa95b22580750a585fae8356bd9c[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Mon Apr 5 17:26:26 2021 -0700

    Only patch for local player

[33mcommit d60563c1c4af17f5d1ccb9deff22808dae50a877[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 4 20:24:07 2021 -0700

    Link to Amplify github page

[33mcommit a75ede7cd027a1b08599c72e0d94b93e7993d8cc[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 4 20:02:30 2021 -0700

    Zero out the defaults for positioning

[33mcommit edc76d102095d07482dd5e4a00931fdf40827bd2[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 4 19:57:19 2021 -0700

    Update DLL reference paths

[33mcommit 932ca93f41d95da67e9d675cdc7376a6c5350b89[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sun Apr 4 17:08:21 2021 -0700

    More reliable head positioning
    
    This updates the positioning of the head to account for the HMD world
    position and should resolve problems of players having their head
    position be offset from the character body. Leaving the ability for the
    player to adjust manually if they need or want a slightly different
    position for preference.

[33mcommit 9f16b08eb2a5604b728a7ca711026fcac464a3fe[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 22:58:41 2021 -0700

    Enable look locomotion
    
    Added a configuration option to allow, while in first person, tying the
    player body rotation and forward motion direction to whatever direction
    the player is looking.

[33mcommit ca6c682e9a00f972b9add43ded7ae443f54ec805[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 20:57:26 2021 -0700

    Add ability to recenter tracking using Home key
    
    If you press the Home key, the tracking is recentered. As an aside, I
    noticed while testing this that recentering resulted in my shifting away
    from the head position of the player. This is probably the root cause of
    why I have been having issues finding the correct center location of the
    player head for first person mode. I probably either need to get the
    tracking data of the headset and center the camera relative to that or I
    need to recenter right before finding the head position. Calculating the
    offset based on current tracking position seems better since there might
    be side effects of recentering.

[33mcommit bc0f5a4fb69c1e9021067318e4d663ea22514158[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 20:28:41 2021 -0700

    Enable repositioning head in vertical direction
    
    Some users reported their vertical head position is wrong. This change
    allows users to reposition the Y axis of their camera with the PageUp
    and PageDown keys and have the value saved back to the config file in
    the same way the X & Z axis positions are saved.
    
    resolves #3

[33mcommit 92bc43e88d797a37d6c3db1722a06fb1b9e28758[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 17:52:59 2021 -0700

    Update readme

[33mcommit eb5ed1c72490c0fd04f87b1216440a02664ef7cd[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 13:53:39 2021 -0700

    Leave hands on by default
    
    Decided to leave the hands on by default and a user can turn them off if
    they really want.

[33mcommit 2b216261c24fe2610451bc94c41a73d4ce8be234[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Sat Apr 3 13:50:56 2021 -0700

    Make IsUsingSteamVRInput fix a Harmony patch
    
    Just use a simple patch for this workaround instead of checking in the
    entire com.valvesoftware.unity.openvr package.

[33mcommit dd03b7a92a9723c8561917c931b39337b5ef2ea3[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 2 18:01:28 2021 -0700

    Dont copy SubsystemRegistration dll. not needed.

[33mcommit 14cf1fab10520c23833144be6f50b358ca477c46[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 2 16:33:16 2021 -0700

    Disable hands by default
    
    Added a configuration option to disable the hands by default. Since
    motion controls aren't working disabling this makes sense, especially
    to avoid any potential conflicts on various devices.

[33mcommit fb43f50f36759480dc230a63979fdcf83c29e024[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 2 16:25:05 2021 -0700

    Fix capitalization in directory name

[33mcommit e43c51c50a34531ef0c6d181b09d4cb36b2f9009[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 2 13:33:56 2021 -0700

    Enable updating head position in game
    
    This adds a new option that allows a user to use the arrow keys to
    reposition the head while playing so that they don't need to continually
    restart the game to tweak the values. The setting is saved back into the
    config file as they update.

[33mcommit 53355bc87e7f2c4f5b67110ee730840c9059ad89[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Fri Apr 2 13:03:25 2021 -0700

    Workaround to avoid incompatibility
    
    This changes the Valve OpenVR plugin to always return true for the
    method used to determine whether or not to use steamvr input. This is
    necessary because I found that there is at least one incompatibility
    with another BepInEx plugin name ConfigurationManager. It isn't clear
    what the root cause is, but calling GetTypes, as is done in
    OpenVRHelpers.cs, causes an exception to be thrown when this plugin is
    loaded first. Since for this mod we want this to always be true, I'll
    just hard code it instead to avoid the exception. It would be best to
    understand the root cause of why the error is thrown, but this seems to
    at least prevent the crash.

[33mcommit a6f4e3911ebbaa33e6378d585b56818d9ea7e4ce[m
Author: Brandon Mousseau <brandon.a.mousseau@gmail.com>
Date:   Thu Apr 1 17:41:54 2021 -0700

    Commit all mod code
    
    This adds all of the mod code up to the current state to this public
    repository. Future work on the mod will continue here.
