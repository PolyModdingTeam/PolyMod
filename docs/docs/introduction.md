# Introduction
Hi! This page will teach you the very basics of modding Polytopia.

# How do I make a mod?
Polytopia mods can be 3 things: folders, `.zip` files and `.polymod` files. 

Mods also have a `manifest.json` file, aswell as other components. Mods are stored in the `Mods` folder of the Polytopia root folder (Polytopia root folder is the folder that holds all the information about the game). 

> [!TIP]
> You can access the root folder in the following ways:
> 
> **On steam:**
> 
> Go to Library --> Polytopia, click on the little gear icon on the right side of the screen, then go Manage --> Browse local files.
>
> **On epic**
>
> [I HAVE NO CLUE LMAO TO FIX!!!!!!!!!!!!]

In order to make a new mod, you have to create a new folder inside the `Mods` folder, name it, and then put a `manifest.json` file in it (more on this later). You can also zip this folder and rename the extension to `.polymod` so you can properly distribute it on the [polymod.dev](https://polymod.dev/) website.

How your file hierarchy should look like:
```
The Battle of Polytopia      (this is your "polytopia root" folder)
--> Mods
    --> ExampleMod1
        --> manifest.json
    --> ExampleMod2.zip
    --> ExampleMod3.polymod
```

# Manifest
The `manifest.json` file contains the metadata (basic information) of your mod, things like its id, name, authors, dependencies, so on. This file is a json file, and looks something like this:
```json
{
    "id": "docs_mod",
    "name": "DocsMod",
    "version": "1.2.3.4",
    "authors": [
        "John Polymod Klipi",
        "Ex-Ploicit Content"
    ],
    "dependencies": [
        {
            "id": "polytopia",
            "min": "2.0.0"
        },
        {
            "id": "another_cool_mod",
            "required": false
        }
    ],
    "description": "This is a very cool mod that turns giants into the one and only Jesus Homonculus"
}
```
> [!TIP]
> If you aren't familiar with the JSON format, try [reading](https://json.org/) a bit about it first.

> [!NOTE]
> We advise you use some code editor app like Visual Studio Code or Notepad++ (or anything, just be comfortable writing JSON).
## Components of a manifest file:
### `id`:
The id of the mod, it's used by other mods to add your mod as a dependency. You can only use lowercase letters and underscores.
### `name`:
The user friendly name of the mod. This is what regular users are going to see. You can put almost anything here.
### `version`:
Tells the user, aswell as the [polymod.dev](https://polymod.dev/) website what version of your mod this is. It only accepts numbers, and you format is so: `x.y.z.w` (`z` and `w` are optional, though `x` and `y` are not).
> [!NOTE]
> It is considered good practice to keep version updated.
### `authors`:
In this array you can credit anyone who has worked on the mod. Again, if you don't know how to format JSON arrays and whatnot, consider watching some tutorial or reading an article!
### `dependencies`:
You can tell PolyMod if your mod depends on any other mod using this field. If you put a mod here as seen in the example, PolyMod will try to load that first, and if it doesn't find that mod, and the dependency isn't set to false as seen in the example, PolyMod will not load your mod, to avoid breaking and crashing and otherwise unwanted behavior.
> [!TIP]
> There exists a "pseudo-mod" with the id of `polytopia`. In this example, we showcase how you can use that in order to set a minimum game version for your mod so it doesn't cause unexpected behavior on older versions! You don't need this for your mod to function, though.
### `description`:
Here you can tell users what your mod is about! When publishing, try to be descriptive cause this is whats going to show up the mod catalogue!
> [!TIP]
> It's generally a good idea to tell your users what dependencies your mod needs.

# Patch

# Sprites

# Localization

# Prefabs

# PolyScript (Advanced)
