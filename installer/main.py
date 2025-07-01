import os
import io
import sys
import shutil
import zipfile
import requests
import threading
import subprocess
import customtkinter
import CTkMessagebox as messagebox

OS = {
    "linux": "linux",
    "linux2": "linux",
    "win32": "win",
    "darwin": "macos",
}[sys.platform]
BEPINEX = "https://polymod.dev/data/bepinex.txt"
POLYMOD = "https://api.github.com/repos/PolyModdingTeam/PolyMod/releases"


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
    prerelease_checkbox.destroy()
    install_button.destroy()
    uninstall_button.destroy()
    progress_bar = customtkinter.CTkProgressBar(app, determinate_speed=50 / 2)
    progress_bar.grid(column=0, row=1, columnspan=2, padx=5, pady=5)
    progress_bar.set(0)
    threading.Thread(target=target, daemon=True, args=(path, )).start()


def install(path):
    to_zip(
        requests.get(
            requests.get(BEPINEX).text.strip().replace("{os}", OS)
        )
    ).extractall(path)
    progress_bar.step()

    for release in requests.get(POLYMOD).json():
        if release["prerelease"] and not prerelease_checkbox.get(): continue
        latest = release
        break
    open(path + "/BepInEx/plugins/PolyMod.dll", "wb").write(
        requests.get(latest["assets"][0]["browser_download_url"]).content
    )
    progress_bar.step()

    customtkinter.CTkButton(app, text="Launch", command=lambda: launch(path)).grid(
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


def launch(path):
    if sys.platform != "win32":
        subprocess.check_call(f"chmod +x {path}/run_bepinex.sh", shell=True)
        subprocess.check_call(f"{path}/run_bepinex.sh {path}/Polytopia.*", shell=True)
        subprocess.check_call(f"xdg-open https://docs.bepinex.dev/articles/advanced/steam_interop.html", shell=True)
    else:
        subprocess.check_call(f"start steam://rungameid/874390", shell=True)
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
prerelease_checkbox = customtkinter.CTkCheckBox(
    app, text="Prerelease", width=1)
install_button = customtkinter.CTkButton(
    app, text="Install", command=lambda: prepare(install))
uninstall_button = customtkinter.CTkButton(
    app, text="Uninstall", command=lambda: prepare(uninstall))

path_entry.grid(column=0, row=0, padx=5, pady=5)
browse_button.grid(column=1, row=0, padx=(0, 5), pady=5)
prerelease_checkbox.grid(column=0, row=1, columnspan=2, padx=5, pady=5)
install_button.grid(column=0, row=2, columnspan=2, padx=5, pady=5)
uninstall_button.grid(column=0, row=3, columnspan=2, padx=5, pady=5)

app.mainloop()
