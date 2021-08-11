==================
 Developer's Guide
==================
This is the overall guide to Courier setup and development

Setup
=====
1. Clone the 'Courier Repo <https://github.com/pennmem/Courier>'_
1. Download 'Unity version 2017.4.35f1 <https://unity3d.com/get-unity/download/archive>'_. The two methods to do this are:
    1. Unity Hub (recommended)
    1. Unity Installer
1. Download the 'Textures folder <https://upenn.box.com/s/7qjifdbbavarik7mraj8a642exhr1l8m>'_ from *system_3_installers/delivery person/Town Constructor 3* in the UPenn Box
1. Put the newly downloaded Textures folder in the *Courier/Assets/Town Constructor 3* folder of the Courier Repo
1. Open the project in Unity
1. Go to *Assets > Town Constructor 3 > scenes* in the project pane on the bottom of unity and double click the *DemoSceneLightSetting3.unity* file to open the map
1. The map will not automatically load the textures correctly (leaving you with the colorless shapes of a town), so you will need to do this manually. In the project pane on the bottom you go into *Town *Assets > Constructor 3 > Materials* and select one, for instance Atlas_B1. When you select, an inspector pane will show up on the right side. Under the Main Maps section the very first item is Albedo. There’s a little circle with a dot in the middle, immediately to the left of the word Albedo, and if you click it a window pops up asking you to Select Texture. This will be linked to all the available textures, which should include the appropriately named textures from the Textures file in Town Constructor 3 - in this case, Atlas_B1. Once you select it the world will suddenly have color again!

Load and Play the Game
======================
1. Go to *Assets > scenes* in the project pane on the bottom of Unity and double click the *MainMenu* file
1. Click the play button at the top




