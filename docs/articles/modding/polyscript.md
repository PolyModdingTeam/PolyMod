# Lua PolyScripting

- [A Brief Introduction to Lua](#a-brief-introduction-to-lua)
  - [Comments](#comments)
  - [Variables and Data Types](#variables-and-data-types)
  - [Tables](#tables)
  - [Functions](#functions)
  - [Control Flow](#control-flow)
- [Core APIs](#core-apis)
  - [Patch](#patch)
  - [Input](#input)
  - [Config and ExposedConfig](#config-and-exposedconfig)
  - [Game and Unity Globals](#game-and-unity-globals)

PolyScript is a powerful scripting system that allows you to create mods for Polytopia using the Lua programming language. You can modify game logic, respond to player input, and interact with a wide range of game and engine components.

For a complete reference of all available C# types and functions, please see the [API documentation](~/api/).

This page assumes you already know the basics of coding. If you already know lua, skip to [Core APIs](#core-apis)
## A Brief Introduction to Lua

If you're new to Lua, here are the basics to get you started.

### Comments

Comments start with `--`. For multi-line comments, use `--[[` and `]]--`.

```lua
-- This is a single-line comment

--[[
This is a
multi-line comment.
]]--
```

### Variables and Data Types

Variables are dynamically typed. You can declare them with `local` to limit their scope or leave it off to make them global (though `local` is generally recommended).

```lua
local myString = "Hello"      -- String
local myNumber = 42           -- Number (double-precision float)
local myBoolean = true        -- Boolean
local myTable = { key = "value" } -- Table (the only data structure in Lua)
local myNil = nil             -- Represents the absence of a value
```

### Tables

Tables are associative arrays and can be used as arrays, dictionaries, or objects.

```lua
-- Array-like table (indices start at 1!)
local list = { "apple", "banana", "orange" }
print(list[1]) -- Outputs "apple"

-- Dictionary-like table
local person = { name = "Bob", age = 30 }
print(person.name) -- Outputs "Bob"
print(person["age"]) -- Outputs 30
```

### Functions

Functions are first-class citizens.

```lua
local function greet(name)
    return "Hello, " .. name .. "!" -- '..' is the string concatenation operator
end

print(greet("PolyModder"))
```

### Control Flow

```lua
local score = 100

if score > 90 then
    print("Excellent!")
elseif score > 60 then
    print("Good job.")
else
    print("Needs improvement.")
end

-- Loop from 1 to 5
for i = 1, 5 do
    print("Iteration: " .. i)
end
```

### Multiple files

You can create multiple polyscripts in a mod. These will all share the same globals and config, but not the same locals.


## Core APIs

PolyScript exposes several global objects to interact with the game.

### `Patch`

The `Patch` API allows you to hook into existing C# methods in the game, effectively changing or extending their behavior. This is the foundation of most mods.

**`Patch.Wrap(methodName, hookFunction)`**

This function "wraps" a target method with your own Lua function.

*   `methodName`: A string representing the full name of the method to patch, in the format `"Namespace.TypeName.MethodName"`.
*   `hookFunction`: A Lua function that will be executed instead of the original method.

Your hook function receives a special function, `orig`, as its first argument. Calling `orig()` will execute the original method's code (or the next patch in the chain).

**Hook Function Signatures:**

*   For instance methods: `function(orig, self, ...)`
*   For static methods: `function(orig, ...)`

Where `...` represents the original arguments passed to the method.

**Example: Logging when a turn starts**

```lua
Patch.Wrap("Polytopia.Game.GameManager.startTurn", function(orig, self, tribe)
    print("A new turn has started for tribe: " .. tribe.Name)
    -- It's crucial to call the original function!
    orig(self, tribe)
end)
```

### `Input`

The `Input` API lets you listen for keyboard events and execute code when keys are pressed.

**`Input.On(keys, callback)`**

*   `keys`: A Lua table containing one or more `KeyCode` values. The last key in the table is the main action key, and any preceding keys are treated as modifiers that must be held down.
*   `callback`: A Lua function to execute when the key combination is pressed.

**Example: Binding an action to `Ctrl + R`**

```lua
-- A list of available KeyCodes can be found in the Unity documentation.
local reloadKey = { KeyCode.LeftControl, KeyCode.R }

Input.On(reloadKey, function()
    print("Reloading!")
    -- Add your reload logic here
end)

-- Binding to a single key
Input.On({ KeyCode.F1 }, function()
    print("Help key pressed!")
end)
```

### `Config` and `ExposedConfig`

PolyScript provides two global objects for saving and loading data:

*   `Config`: A key-value store private to your mod. Use this for internal data that should persist across game restarts.
*   `ExposedConfig`: A key-value store for settings that the **user can configure**. This data is typically exposed in a settings menu or a user-editable file.

These objects behave like standard Lua tables. **Important:** After changing a value, you must call `SaveChanges()` to write the data to disk.

**Example: Storing a user preference**

```lua
-- Set a default value if one doesn't exist
if ExposedConfig.playerGreeting == nil then
    ExposedConfig.playerGreeting = "Hello, player!"
end

-- To save any changes you make to the configuration:
ExposedConfig.SaveChanges()

-- Read the value on startup
print(ExposedConfig.playerGreeting)
```

### Game and Unity Globals

Many of Unity's core types and the game's own types are available as globals in the Lua environment. This allows you to create new objects and call static methods directly.

**Available Unity Globals (Examples):**
`Vector2`, `Vector3`, `GameObject`, `Color`, `Mathf`, `Time`

**Example: Creating a new Vector3**

```lua
local myVector = Vector3.new(10, 20, 0)
print("Created a vector: " .. myVector.x .. ", " .. myVector.y)
```
