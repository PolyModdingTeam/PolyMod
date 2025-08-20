# Creating your first mod

## Step 1: Setting up your environment
Before you start writing mods, you should ensure you have the following:
- A text editor. Notepad or Text Editor will be enough.
> [!TIP]
> Using a text editor meant for writing code, like [vscode](https://code.visualstudio.com/) or Kate(preinstalled on most linux distros) is a lot easier when writing and editing code. However, this isn't required.

[Install PolyMod.](../using/installing.md)
Inside your mods folder, create a folder, e.g. `example`. If you have an IDE, open that folder with your IDE. Else, open the folder with file manager(or finder, or dolphin etc.)

## Step 2: Writing the manifest
Create `manifest.json` and paste the following into it:
```json
{
  "id" : "my_first_mod",
  "name": "My first mod",
  "version": "1.0.0",
  "authors": ["your-name"],
  "dependencies": [
    {
      "id": "polytopia"
    }
  ]
}
```

## Step 3: Creating a patch
Create `patch.json` and paste the following into it:
```
{
  "unitData" : {
    "warrior" : {
      "attack" : 100
    }
  }
}
```
This will make the warrior have a large attack value. For more information about GLD patching, see (gld.md)

## Step 4: loading the mod
Start the game. Thats it! You should see your mod listed under the polymod hub.

## Step 5: publishing your mod
Once you have finished your mod, you may want to bundle it into a single file for easier sharing. Here's how to do that:
1. zip the folder of the mod
2. rename the zipfile to `example.polymod`