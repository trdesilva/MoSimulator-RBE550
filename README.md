# RBE550 MoSim Fork
This repo is a fork of the public MoSim modding repo for a WPI RBE550 semester project.

## How do I run it?
Either download a 2025 release build of MoSim and follow the [modpack installation instructions](https://docs.mosimulator.com/modding/lynk-walkthrough/mod-building) from the official docs, or do the following to run under a debugger in the Unity Editor:

1. Open the Reefscape scene under (project root)/Assets/Scenes. You can choose which robots will be spawned using the fields under Reefscape/RobotSpawnController.
1. Go to File > Build Settings... in the Unity Editor.
1. Enable the checkboxes for Copy PDB Files, Development Build, Script Debugging, and Wait For Managed Debugger.
1. Click Build And Run.
1. Choose a location for the build artifacts for this configuration to be written. I put them under (project root)/Build/Debug.

Once you've done this, you can attach Visual Studio to the Unity Editor and use the Play, Pause, and Stop buttons to run MoSim inside the editor. You can also run the executable in the location you chose in the last step and attach a debugger to it directly.

## Modding Documentation
All of the modding documentation is located on the website here: [Modding Documentation](https://docs.mosimulator.com/).

<br>
Note: MoSimulator's source code is protected by a custom proprietary license. By downloading or interacting with this repository, you agree to the terms outlined in the LICENSE file.
