import os
import io
import sys
import shutil
import zipfile
import requests
import threading
import customtkinter
import CTkMessagebox as messagebox

OS = {
    "linux": "linux",
    "linux2": "linux",
    "win32": "win",
    "darwin": "macos",
}[sys.platform]
BEPINEX = f"733/BepInEx-Unity.IL2CPP-{OS}-x64-6.0.0-be.733%2B995f049"
POLYMOD = "https://github.com/PolyModdingTeam/PolyMod/releases/latest/download/PolyMod.dll"


def resource_path(path):
    try:
        base_path = sys._MEIPASS
    except Exception:
        base_path = os.path.abspath(".")
    return os.path.join(base_path, path)


def to_zip(request: requests.Response):
    return zipfile.ZipFile(io.BytesIO(request.content))


def browse():
    global path_entry
    path_entry.delete(0, customtkinter.END)
    path_entry.insert(0, customtkinter.filedialog.askdirectory())


def prepare(target):
    global progress_bar
    path = path_entry.get()
    try:
        if "Polytopia_Data" not in os.listdir(path):
            raise FileNotFoundError
    except FileNotFoundError:
        messagebox.CTkMessagebox(
            title="Error",
            message="The folder does not exist or is not valid!",
            icon="cancel",
            width=100,
            height=50
        )
        return
    path_entry.configure(state=customtkinter.DISABLED)
    browse_button.configure(state=customtkinter.DISABLED)
    install_button.destroy()
    uninstall_button.destroy()
    progress_bar = customtkinter.CTkProgressBar(app, determinate_speed=50 / 2)
    progress_bar.grid(column=0, row=1, columnspan=2, padx=5, pady=5)
    progress_bar.set(0)
    threading.Thread(target=target, daemon=True, args=(path, )).start()


def install(path):
    to_zip(
        requests.get(
            f"https://builds.bepinex.dev/projects/bepinex_be/{BEPINEX}.zip"
        )
    ).extractall(path)
    progress_bar.step()

    open(path + "/BepInEx/plugins/PolyMod.dll", "wb").write(
        requests.get(POLYMOD).content
    )
    progress_bar.step()

    customtkinter.CTkButton(app, text="Launch", command=launch).grid(
        column=0, row=2, columnspan=2, padx=5, pady=5
    )


def uninstall(path):
    dirs = [
        "BepInEx",
        "dotnet",
    ]
    files = [
        ".doorstop_version",
        "changelog.txt",
        "doorstop_config.ini",
        "winhttp.dll",  # windows
        "libdoorstop.so",  # linux
        "libdoorstop.dylib",  # mac
        "run_bepinex.sh",  # linux + mac
    ]
    for dir in dirs:
        shutil.rmtree(path + "/" + dir, True)
    progress_bar.step()
    for file in files:
        try:
            os.remove(path + "/" + file)
        except FileNotFoundError:
            ...
    progress_bar.step()
    customtkinter.CTkButton(app, text="Quit", command=quit).grid(
        column=0, row=2, columnspan=2, padx=5, pady=5
    )


def launch():
    os.startfile("steam://rungameid/874390")
    quit()


def quit():
    app.destroy()
    sys.exit()


app = customtkinter.CTk()
app.title("PolyMod")
if OS != "linux":
    app.iconbitmap(default=resource_path("icon.ico"))
app.resizable(False, False)

path_entry = customtkinter.CTkEntry(
    app, placeholder_text="Game path", width=228)
browse_button = customtkinter.CTkButton(
    app, text="Browse", command=browse, width=1)
install_button = customtkinter.CTkButton(
    app, text="Install", command=lambda: prepare(install))
uninstall_button = customtkinter.CTkButton(
    app, text="Uninstall", command=lambda: prepare(uninstall))

path_entry.grid(column=0, row=0, padx=5, pady=5)
browse_button.grid(column=1, row=0, padx=(0, 5), pady=5)
install_button.grid(column=0, row=1, columnspan=2, padx=5, pady=5)
uninstall_button.grid(column=0, row=2, columnspan=2, padx=5, pady=5)

app.mainloop()
