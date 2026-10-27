# FPS CORE

### REMAPPING KEYBIND INPUT

- Using the Demo Scene: `FPSCore_Demo`
- Go To `InputManager` -> There should be an `Input Remapper` Component Attached.
- Right Click `Input Remapper` Component then click `Populate From Asset`
  - This button automatically detects if the game using a custom input asset from the `Input Manager`
  - It will auto-populate all of the input action mappings into the `Designer Default Entries`.
  - This is where you can adjust keybinds in the editor, without having to adjust it inside the asset itself.
    - You can toggle keybinds as: `Hidden` and/or `Locked`
    - `Keybinds` can have multiple defaults.
    - `Keybinds` are automatically**** reflected in the built-in Keybind UI (`F1`)
  - If your project allows it, you can switch the UI to scene mode in the `Keybind Menu` component inherited by the `Input UI` object.
  - This allows you to reference the following fields needed for the Keybind UI:
    - **Panel Root**
      - Root object of your UI. This is where your Keybind menu lives.
    - **Row Container**
      - Root object of Keybind Row Container. This is a scroll rect object, specifically the object that contains the vertical scroll container.
    - **Row Prefab**
      - `Keybind Row` Object. Your Object that displays your individual keybinds as rows.
      - It needs the following fields:
        - Action Label
        - Bind Button
        - Bind Text
        - Reset Button
    - **Reset All Button**
    - **Close Button**
    - Listening Overlay (OPTIONAL)
      - A stretched UI panel / image object in transparent black
    - **Listening Text (OPTIONAL)**
      - A TextMeshPro object inside the overlay that says "Listening for input..." or similar.
    - **Status Text (OPTIONAL)**
      - A TextMeshPro object that displays messages such as:
        - "Swapped Keys.."
        - "Locked Keys.."
        - etc.

## Interactor (Main Addon)

### About

* Interactor is the main addon for FPS Core, which allows objects to be interacted.
* Each `Interactable` object has an `Interact` call you can use to invoke game-specific events for your project such as the following:
  * Opening a Computer Terminal
  * Interacting with a door
  * etc.
* To add this addon: Go to `Player (Prefab)` -> `First Person Controller (Component)` -> Scroll down, there should be an `Addons` Foldout that lists the `Interactor` component as a button.
* You can adjust the `Interact Range` in the inspector based on your needs.

## Carrier (Addon)

### About

* Carrier is an addon for FPS Core, which allows objects to be carried by the player.
* To add this addon: Go to `Interactable (Object)` -> Scroll down, there should be an `Addons` Foldout that lists the `Carrier (Addon)` component as a button.
* Carrier currently has 1 sub-addon:
  * Carrier Ticker (Optimization)
    * Optimization helper script that makes carrier objects have a fixed tick rate, and rate-limits it.
    * To add this sub-addon: Go to `Carrier (Component)` -> Scroll down, there should be an `Addons` Foldout that lists the `Carrier Ticker` component.

## Hotbar (Addon)

### About

* Hotbar is an addon for FPS Core, which adds hotbar inventory for the player.
* To add this addon: Go to your `Player (Prefab)` -> `First Person Controller (Component)` -> `Add Hotbar` button
* To Change the number of Hotbor Slots: Go to `Hotbar (Component)` -> Change the `Size` variable to your liking. Max Slots a player can have is 9.
