#!/usr/bin/env python3
"""
Fatigue Setup Generator
A Windows 95 style graphical user interface for generating fatigue loading scenarios
for durability simulations in FatLab.
"""

import os
import sys
import json
import tkinter as tk
from tkinter import ttk, filedialog, messagebox, scrolledtext
import numpy as np
import pandas as pd
import matplotlib.pyplot as plt
from matplotlib.backends.backend_tkagg import FigureCanvasTkAgg
from scipy import stats
import argparse
from scipy import io as sio
import openpyxl
import random
from scipy import signal
import scipy.io

# Check if running in demo mode
parser = argparse.ArgumentParser(description='Fatigue Setup Generator')
parser.add_argument('--demo', action='store_true', help='Run in demo mode with sample data')
args = parser.parse_args()

# Constants for Windows 95 style
WIN95_GRAY = "#c0c0c0"
WIN95_DARK = "#808080"
WIN95_LIGHT = "#dfdfdf"
WIN95_BLUE = "#000080"
WIN95_WHITE = "#ffffff"
WIN95_BLACK = "#000000"
WIN95_FONT = ("MS Sans Serif", 8)
WIN95_FONT_BOLD = ("MS Sans Serif", 8, "bold")

class Win95Style:
    """Windows 95 style utilities for tkinter widgets"""
    
    @staticmethod
    def configure_styles():
        """Configure ttk styles to match Windows 95 appearance"""
        style = ttk.Style()
        
        # Configure TButton style
        style.configure("Win95.TButton",
                        background=WIN95_GRAY,
                        relief="raised",
                        font=WIN95_FONT)
        
        # Configure TFrame style
        style.configure("Win95.TFrame",
                        background=WIN95_GRAY)
        
        # Configure TLabel style
        style.configure("Win95.TLabel",
                        background=WIN95_GRAY,
                        font=WIN95_FONT)
        
        # Configure TEntry style
        style.configure("Win95.TEntry",
                        fieldbackground=WIN95_WHITE,
                        font=WIN95_FONT)
        
        # Configure TNotebook style
        style.configure("Win95.TNotebook",
                        background=WIN95_GRAY,
                        tabmargins=[2, 5, 2, 0])
        
        style.configure("Win95.TNotebook.Tab",
                        background=WIN95_GRAY,
                        padding=[5, 1],
                        font=WIN95_FONT)
        
        # Configure TCombobox style
        style.configure("Win95.TCombobox",
                        background=WIN95_GRAY,
                        fieldbackground=WIN95_WHITE,
                        font=WIN95_FONT)
        
        # Configure TCheckbutton style
        style.configure("Win95.TCheckbutton",
                        background=WIN95_GRAY,
                        font=WIN95_FONT)
        
        # Configure TRadiobutton style
        style.configure("Win95.TRadiobutton",
                        background=WIN95_GRAY,
                        font=WIN95_FONT)
        
        # Configure TLabelframe style
        style.configure("Win95.TLabelframe",
                        background=WIN95_GRAY,
                        font=WIN95_FONT)
        
        style.configure("Win95.TLabelframe.Label",
                        background=WIN95_GRAY,
                        font=WIN95_FONT)

class FatigueSetupGenerator:
    """Main application class for Fatigue Setup Generator"""
    
    def __init__(self, root, demo_mode=False):
        """Initialize the application"""
        self.root = root
        self.demo_mode = demo_mode
        self.setup_window()
        self.create_variables()
        
        # Initialize status_var before it's used in any method
        self.status_var = tk.StringVar()
        self.status_var.set("Ready")
        
        self.create_status_bar()
        self.create_widgets()
        self.create_menu()
        
        # Load demo data if in demo mode
        if self.demo_mode:
            self.load_demo_data()
    
    def setup_window(self):
        """Configure the main window"""
        self.root.title("Fatigue Setup Generator")
        self.root.geometry("800x600")
        self.root.minsize(640, 480)
        self.root.configure(background=WIN95_GRAY)
        
        # Set icon if available
        icon_path = "fatigue_icon.ico" if os.path.exists("fatigue_icon.ico") else "fatigue_icon.png"
        if os.path.exists(icon_path):
            try:
                self.root.iconbitmap(icon_path) if icon_path.endswith(".ico") else None
            except:
                pass
    
    def create_variables(self):
        """Create tkinter variables for form fields"""
        # Load characteristics variables
        self.mean_load = tk.DoubleVar(value=0.0)
        self.load_amplitude = tk.DoubleVar(value=1.0)
        self.load_ratio = tk.DoubleVar(value=0.1)
        self.waveform_type = tk.StringVar(value="Sinusoidal")
        self.frequency = tk.DoubleVar(value=1.0)
        self.phase_shift = tk.DoubleVar(value=0.0)
        self.total_cycles = tk.IntVar(value=1000)  # Total number of cycles
        self.sampling_rate = 1000  # Points per second for waveform generation
        
        # Reference case loads
        self.ref_pos_load = tk.DoubleVar(value=600.0)  # Reference positive load
        self.ref_neg_load = tk.DoubleVar(value=-300.0)  # Reference negative load
        
        # Target case loads
        self.target_max_load = tk.DoubleVar(value=600.0)  # Target maximum load
        self.target_min_load = tk.DoubleVar(value=-600.0)  # Target minimum load
        
        # Use reference/target mode
        self.use_reference_mode = tk.BooleanVar(value=False)  # Whether to use reference/target mode
        
        # Stochastic variables
        self.enable_stochastic = tk.BooleanVar(value=False)
        self.distribution_type = tk.StringVar(value="Gaussian")
        self.distribution_param1 = tk.DoubleVar(value=0.0)  # Mean for Gaussian, Scale for others
        self.distribution_param2 = tk.DoubleVar(value=1.0)  # Std for Gaussian, Shape for Weibull, etc.
        self.noise_level = tk.DoubleVar(value=0.1)  # Relative noise level (0.1 = 10%)
        
        # Output file variables
        self.output_file = tk.StringVar(value="fatigue_setup.xlsx")
        self.export_format = tk.StringVar(value="excel")
        self.num_repetitions = tk.IntVar(value=1)
        self.export_description = tk.StringVar(value="Fatigue Loading Scenario")
        
        # Loading scenarios list for superposition
        self.loading_scenarios = []
        
        # Sequential loading variables
        self.sequential_scenarios = []
        self.transition_type = tk.StringVar(value="Linear")
        self.transition_cycles = tk.IntVar(value=100)  # Number of cycles for transition
        self.match_phase = tk.BooleanVar(value=True)  # Match phase at transitions
        self.is_sequential_mode = tk.BooleanVar(value=False)
    
    def create_widgets(self):
        """Create the application widgets"""
        # Main frame
        self.main_frame = ttk.Frame(self.root, style="Win95.TFrame")
        self.main_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Create a horizontal split with parameters on left and graph on right
        self.paned_window = ttk.PanedWindow(self.main_frame, orient=tk.HORIZONTAL)
        self.paned_window.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Left frame for parameters
        self.left_frame = ttk.Frame(self.paned_window, style="Win95.TFrame")
        
        # Right frame for graph
        self.right_frame = ttk.Frame(self.paned_window, style="Win95.TFrame")
        
        # Add frames to paned window
        self.paned_window.add(self.left_frame, weight=1)
        self.paned_window.add(self.right_frame, weight=2)
        
        # Create a notebook (tabbed interface) for the left side
        self.notebook = ttk.Notebook(self.left_frame, style="Win95.TNotebook")
        self.notebook.pack(fill=tk.BOTH, expand=True)
        
        # Create tabs
        self.load_tab = ttk.Frame(self.notebook, style="Win95.TFrame")
        self.stochastic_tab = ttk.Frame(self.notebook, style="Win95.TFrame")
        self.superposition_tab = ttk.Frame(self.notebook, style="Win95.TFrame")
        self.sequential_tab = ttk.Frame(self.notebook, style="Win95.TFrame")
        self.export_tab = ttk.Frame(self.notebook, style="Win95.TFrame")
        
        self.notebook.add(self.load_tab, text="Load Characteristics")
        self.notebook.add(self.stochastic_tab, text="Stochastic Aspects")
        self.notebook.add(self.superposition_tab, text="Superposition")
        self.notebook.add(self.sequential_tab, text="Sequential Loading")
        self.notebook.add(self.export_tab, text="Export")
        
        # Create the load characteristics form
        self.create_load_form()
        
        # Create the stochastic aspects form
        self.create_stochastic_form()
        
        # Create the superposition form
        self.create_superposition_form()
        
        # Create the sequential loading form
        self.create_sequential_form()
        
        # Create the export form
        self.create_export_form()
        
        # Create the graph preview on the right side
        self.create_graph_preview()
        
        # Create the action buttons
        self.create_action_buttons()
    
    def create_load_form(self):
        """Create the load characteristics form"""
        load_frame = ttk.LabelFrame(self.load_tab, text="Load Characteristics", padding="10")
        load_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Mode selection
        mode_frame = ttk.Frame(load_frame)
        mode_frame.pack(side="top", fill="x", pady=(0, 10))
        ttk.Checkbutton(mode_frame, text="Use Reference/Target Mode", 
                       variable=self.use_reference_mode,
                       command=self.toggle_load_mode).pack(side="left")
        
        # Standard mode fields
        self.standard_frame = ttk.Frame(load_frame)
        self.standard_frame.pack(fill="both", expand=True)
        
        # Create a grid within the standard frame
        ttk.Label(self.standard_frame, text="Mean Load:").grid(row=0, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(self.standard_frame, textvariable=self.mean_load).grid(row=0, column=1, padx=5, pady=2)
        
        ttk.Label(self.standard_frame, text="Load Amplitude:").grid(row=1, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(self.standard_frame, textvariable=self.load_amplitude).grid(row=1, column=1, padx=5, pady=2)
        
        ttk.Label(self.standard_frame, text="Load Ratio:").grid(row=2, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(self.standard_frame, textvariable=self.load_ratio).grid(row=2, column=1, padx=5, pady=2)
        
        # Reference/Target mode fields
        self.reference_frame = ttk.Frame(load_frame)
        self.reference_frame.pack(fill="both", expand=True)
        
        # Reference loads
        ref_frame = ttk.LabelFrame(self.reference_frame, text="Reference Case", padding="5")
        ref_frame.pack(side="left", fill="both", expand=True, padx=5, pady=5)
        
        # Use grid inside the ref_frame
        ttk.Label(ref_frame, text="Positive Load:").grid(row=0, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(ref_frame, textvariable=self.ref_pos_load).grid(row=0, column=1, padx=5, pady=2)
        
        ttk.Label(ref_frame, text="Negative Load:").grid(row=1, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(ref_frame, textvariable=self.ref_neg_load).grid(row=1, column=1, padx=5, pady=2)
        
        # Target loads
        target_frame = ttk.LabelFrame(self.reference_frame, text="Target Case", padding="5")
        target_frame.pack(side="right", fill="both", expand=True, padx=5, pady=5)
        
        # Use grid inside the target_frame
        ttk.Label(target_frame, text="Maximum Load:").grid(row=0, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(target_frame, textvariable=self.target_max_load).grid(row=0, column=1, padx=5, pady=2)
        
        ttk.Label(target_frame, text="Minimum Load:").grid(row=1, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(target_frame, textvariable=self.target_min_load).grid(row=1, column=1, padx=5, pady=2)
        
        # Common fields
        common_frame = ttk.Frame(load_frame)
        common_frame.pack(side="bottom", fill="x", pady=10)
        
        # Use grid inside the common_frame
        ttk.Label(common_frame, text="Waveform Type:").grid(row=0, column=0, sticky="w", padx=5, pady=2)
        ttk.Combobox(common_frame, textvariable=self.waveform_type, 
                    values=["Sinusoidal", "Triangular", "Square"]).grid(row=0, column=1, padx=5, pady=2)
        
        ttk.Label(common_frame, text="Frequency (Hz):").grid(row=1, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(common_frame, textvariable=self.frequency).grid(row=1, column=1, padx=5, pady=2)
        
        ttk.Label(common_frame, text="Phase Shift (degrees):").grid(row=2, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(common_frame, textvariable=self.phase_shift).grid(row=2, column=1, padx=5, pady=2)
        
        ttk.Label(common_frame, text="Total Cycles:").grid(row=3, column=0, sticky="w", padx=5, pady=2)
        ttk.Entry(common_frame, textvariable=self.total_cycles).grid(row=3, column=1, padx=5, pady=2)
        
        # Initialize visibility
        self.toggle_load_mode()
        
        return load_frame
        
    def toggle_load_mode(self):
        """Toggle between standard and reference/target mode"""
        if self.use_reference_mode.get():
            self.standard_frame.pack_forget()
            self.reference_frame.pack(fill="both", expand=True)
        else:
            self.standard_frame.pack(fill="both", expand=True)
            self.reference_frame.pack_forget()
    
    def create_stochastic_form(self):
        """Create the stochastic aspects form"""
        # Create a frame for the form
        form_frame = ttk.LabelFrame(self.stochastic_tab, text="Stochastic Parameters", style="Win95.TLabelframe")
        form_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Create a grid layout for the form
        row = 0
        
        # Enable stochastic aspects
        ttk.Checkbutton(form_frame, text="Enable Stochastic Aspects", variable=self.enable_stochastic, 
                        style="Win95.TCheckbutton", command=self.update_preview).grid(
                        row=row, column=0, columnspan=2, sticky=tk.W, padx=5, pady=5)
        row += 1
        
        # Distribution Type
        ttk.Label(form_frame, text="Probability Distribution:", style="Win95.TLabel").grid(
            row=row, column=0, sticky=tk.W, padx=5, pady=5)
        distribution_combo = ttk.Combobox(form_frame, textvariable=self.distribution_type, style="Win95.TCombobox")
        distribution_combo['values'] = ('Gaussian', 'Weibull', 'Log-normal', 'Exponential')
        distribution_combo.grid(row=row, column=1, sticky=tk.W, padx=5, pady=5)
        distribution_combo.bind('<<ComboboxSelected>>', self.update_distribution_params)
        row += 1
        
        # Parameter 1 (Mean/Scale)
        self.param1_label = ttk.Label(form_frame, text="Mean:", style="Win95.TLabel")
        self.param1_label.grid(row=row, column=0, sticky=tk.W, padx=5, pady=5)
        ttk.Entry(form_frame, textvariable=self.distribution_param1, width=15, style="Win95.TEntry").grid(
            row=row, column=1, sticky=tk.W, padx=5, pady=5)
        row += 1
        
        # Parameter 2 (Std/Shape)
        self.param2_label = ttk.Label(form_frame, text="Standard Deviation:", style="Win95.TLabel")
        self.param2_label.grid(row=row, column=0, sticky=tk.W, padx=5, pady=5)
        ttk.Entry(form_frame, textvariable=self.distribution_param2, width=15, style="Win95.TEntry").grid(
            row=row, column=1, sticky=tk.W, padx=5, pady=5)
        row += 1
        
        # Noise Level
        ttk.Label(form_frame, text="Noise Level (%):", style="Win95.TLabel").grid(
            row=row, column=0, sticky=tk.W, padx=5, pady=5)
        ttk.Entry(form_frame, textvariable=self.noise_level, width=15, style="Win95.TEntry").grid(
            row=row, column=1, sticky=tk.W, padx=5, pady=5)
        row += 1
        
        # Add a button to update the preview
        ttk.Button(form_frame, text="Update Preview", command=self.update_preview, style="Win95.TButton").grid(
            row=row, column=0, columnspan=2, pady=10)
        
        # Initialize the parameter labels
        self.update_distribution_params()
    
    def update_distribution_params(self, event=None):
        """Update the parameter labels based on the selected distribution"""
        distribution = self.distribution_type.get()
        
        if distribution == "Gaussian":
            self.param1_label.config(text="Mean:")
            self.param2_label.config(text="Standard Deviation:")
            if self.distribution_param2.get() <= 0:
                self.distribution_param2.set(1.0)
        elif distribution == "Weibull":
            self.param1_label.config(text="Scale:")
            self.param2_label.config(text="Shape:")
            if self.distribution_param1.get() <= 0:
                self.distribution_param1.set(1.0)
            if self.distribution_param2.get() <= 0:
                self.distribution_param2.set(1.0)
        elif distribution == "Log-normal":
            self.param1_label.config(text="Log-Mean:")
            self.param2_label.config(text="Log-Std:")
            if self.distribution_param2.get() <= 0:
                self.distribution_param2.set(0.5)
        elif distribution == "Exponential":
            self.param1_label.config(text="Scale:")
            self.param2_label.config(text="")  # Exponential has only one parameter
            if self.distribution_param1.get() <= 0:
                self.distribution_param1.set(1.0)
        
        # Update the preview if the figure exists
        if hasattr(self, 'ax'):
            self.update_preview()
    
    def create_graph_preview(self):
        """Create the graph preview area"""
        # Create a frame for the graph
        graph_frame = ttk.LabelFrame(self.right_frame, text="Load Waveform Preview", style="Win95.TLabelframe")
        graph_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Create a matplotlib figure
        self.fig = plt.Figure(figsize=(6, 4), dpi=100)
        self.ax = self.fig.add_subplot(111)
        
        # Create a canvas to display the figure
        self.canvas = FigureCanvasTkAgg(self.fig, master=graph_frame)
        self.canvas.get_tk_widget().pack(fill=tk.BOTH, expand=True)
        
        # Initial plot
        self.update_preview()
    
    def create_export_form(self):
        """Create the export form"""
        # Create a frame for the form
        export_frame = ttk.LabelFrame(self.export_tab, text="Export Options", style="Win95.TLabelframe")
        export_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Output file
        ttk.Label(export_frame, text="Output File:", style="Win95.TLabel").grid(row=0, column=0, sticky=tk.W, padx=5, pady=5)
        ttk.Entry(export_frame, textvariable=self.output_file, width=40, style="Win95.TEntry").grid(row=0, column=1, sticky=tk.W, padx=5, pady=5)
        ttk.Button(export_frame, text="Browse...", command=self.browse_output_file, style="Win95.TButton").grid(row=0, column=2, padx=5, pady=5)
        
        # Export format selection
        ttk.Label(export_frame, text="Export Format:", style="Win95.TLabel").grid(row=1, column=0, sticky=tk.W, padx=5, pady=5)
        format_frame = ttk.Frame(export_frame, style="Win95.TFrame")
        format_frame.grid(row=1, column=1, sticky=tk.W, padx=5, pady=5)
        
        # Add export format radio buttons
        self.export_format = tk.StringVar(value="excel")
        ttk.Radiobutton(format_frame, text="Excel (.xlsx)", variable=self.export_format, 
                        value="excel", style="Win95.TRadiobutton").pack(side=tk.LEFT, padx=5)
        ttk.Radiobutton(format_frame, text="MATLAB (.mat)", variable=self.export_format, 
                        value="matlab", style="Win95.TRadiobutton").pack(side=tk.LEFT, padx=5)
        
        # Number of repetitions for MATLAB export
        ttk.Label(export_frame, text="Number of Repetitions:", style="Win95.TLabel").grid(row=2, column=0, sticky=tk.W, padx=5, pady=5)
        self.num_repetitions = tk.IntVar(value=1)
        ttk.Entry(export_frame, textvariable=self.num_repetitions, width=10, style="Win95.TEntry").grid(row=2, column=1, sticky=tk.W, padx=5, pady=5)
        
        # Description for MATLAB export
        ttk.Label(export_frame, text="Description:", style="Win95.TLabel").grid(row=3, column=0, sticky=tk.W, padx=5, pady=5)
        self.export_description = tk.StringVar(value="Fatigue Loading Scenario")
        ttk.Entry(export_frame, textvariable=self.export_description, width=40, style="Win95.TEntry").grid(row=3, column=1, sticky=tk.W, padx=5, pady=5)
        
        # Export button
        ttk.Button(export_frame, text="Export", command=self.export_data, style="Win95.TButton").grid(row=4, column=0, columnspan=3, pady=10)
    
    def create_action_buttons(self):
        """Create the action buttons at the bottom of the window"""
        button_frame = ttk.Frame(self.main_frame, style="Win95.TFrame")
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # Create buttons with 3D effect
        ttk.Button(button_frame, text="Update Preview", command=self.update_preview, style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Export", command=self.export_data, style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Reset Form", command=self.reset_form, style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Exit", command=self.root.quit, style="Win95.TButton").pack(side=tk.RIGHT, padx=5)
    
    def create_menu(self):
        """Create the application menu"""
        menubar = tk.Menu(self.root)
        
        # File menu
        file_menu = tk.Menu(menubar, tearoff=0)
        file_menu.add_command(label="New", command=self.reset_form)
        file_menu.add_command(label="Open Configuration...", command=self.load_configuration)
        file_menu.add_command(label="Save Configuration...", command=self.save_configuration)
        file_menu.add_separator()
        file_menu.add_command(label="Export to Excel...", command=lambda: self.export_data_with_format("excel"))
        file_menu.add_command(label="Export to MATLAB...", command=lambda: self.export_data_with_format("matlab"))
        file_menu.add_separator()
        file_menu.add_command(label="Exit", command=self.root.quit)
        menubar.add_cascade(label="File", menu=file_menu)
        
        # Edit menu
        edit_menu = tk.Menu(menubar, tearoff=0)
        edit_menu.add_command(label="Reset Form", command=self.reset_form)
        menubar.add_cascade(label="Edit", menu=edit_menu)
        
        # View menu
        view_menu = tk.Menu(menubar, tearoff=0)
        view_menu.add_command(label="Update Preview", command=self.update_preview)
        menubar.add_cascade(label="View", menu=view_menu)
        
        # Superposition menu
        superposition_menu = tk.Menu(menubar, tearoff=0)
        superposition_menu.add_command(label="Add Current Setup", command=self.add_current_scenario)
        superposition_menu.add_command(label="Remove Selected Scenario", command=self.remove_selected_scenario)
        superposition_menu.add_command(label="Clear All Scenarios", command=self.clear_all_scenarios)
        superposition_menu.add_separator()
        superposition_menu.add_command(label="Preview Superposition", command=self.preview_superposition)
        menubar.add_cascade(label="Superposition", menu=superposition_menu)
        
        # Sequential Loading menu
        sequential_menu = tk.Menu(menubar, tearoff=0)
        sequential_menu.add_command(label="Add Current Setup", command=self.add_sequential_scenario)
        sequential_menu.add_command(label="Remove Selected Scenario", command=self.remove_sequential_scenario)
        sequential_menu.add_command(label="Move Scenario Up", command=self.move_scenario_up)
        sequential_menu.add_command(label="Move Scenario Down", command=self.move_scenario_down)
        sequential_menu.add_command(label="Clear All Scenarios", command=self.clear_sequential_scenarios)
        sequential_menu.add_separator()
        sequential_menu.add_command(label="Preview Sequential Loading", command=self.preview_sequential)
        menubar.add_cascade(label="Sequential", menu=sequential_menu)
        
        # Help menu
        help_menu = tk.Menu(menubar, tearoff=0)
        help_menu.add_command(label="About", command=self.show_about)
        menubar.add_cascade(label="Help", menu=help_menu)
        
        self.root.config(menu=menubar)
    
    def create_status_bar(self):
        """Create the status bar at the bottom of the window"""
        status_frame = ttk.Frame(self.root, relief=tk.SUNKEN, style="Win95.TFrame")
        status_frame.pack(side=tk.BOTTOM, fill=tk.X)
        
        status_label = ttk.Label(status_frame, textvariable=self.status_var, style="Win95.TLabel", anchor=tk.W)
        status_label.pack(fill=tk.X, padx=5, pady=2)
    
    def update_preview(self, event=None):
        """Update the preview graph with the current parameters"""
        try:
            # Clear the previous plot
            self.ax.clear()
            
            # Get the waveform parameters
            waveform_type = self.waveform_type.get()
            frequency = self.frequency.get()
            total_cycles = self.total_cycles.get()
            
            # Generate time points for two cycles (for preview)
            preview_cycles = min(2, total_cycles)  # Show at most 2 cycles in preview
            preview_ratio = preview_cycles / total_cycles
            
            # Generate the full waveform
            time, y = self.generate_waveform()
            
            if time is None or y is None:
                return
                
            # Slice to only the preview portion
            preview_points = int(len(time) * preview_ratio)
            preview_time = time[:preview_points]
            preview_y = y[:preview_points]
            
            # Apply stochastic aspects if enabled
            if self.enable_stochastic.get():
                # Get stochastic parameters
                distribution = self.distribution_type.get()
                param1 = self.distribution_param1.get()
                param2 = self.distribution_param2.get()
                noise_level = self.noise_level.get() / 100.0  # Convert from percentage
                
                # Determine the amplitude to scale the noise
                if self.use_reference_mode.get():
                    amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                else:
                    amplitude = self.load_amplitude.get()
                
                # Generate random noise based on the selected distribution
                if distribution == "Gaussian":
                    noise = stats.norm.rvs(loc=param1, scale=param2, size=len(preview_time))
                elif distribution == "Weibull":
                    noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(preview_time))
                elif distribution == "Log-normal":
                    noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(preview_time))
                elif distribution == "Exponential":
                    noise = stats.expon.rvs(scale=param1, size=len(preview_time))
                
                # Scale the noise to the desired level relative to the amplitude
                scaled_noise = noise * amplitude * noise_level
                
                # Add the noise to the waveform
                preview_y = preview_y + scaled_noise
            
            # Plot the waveform
            self.ax.plot(preview_time, preview_y, 'b-')
            
            # Set labels and title
            self.ax.set_xlabel('Time (s)')
            self.ax.set_ylabel('Load')
            
            # Create appropriate title
            if self.use_reference_mode.get():
                title = f'{waveform_type} Waveform (Reference/Target Mode)'
            else:
                title = f'{waveform_type} Waveform'
                
            if self.enable_stochastic.get():
                title += f' with {self.distribution_type.get()} Distribution'
            self.ax.set_title(title)
            
            # Add grid
            self.ax.grid(True)
            
            # Update the canvas
            self.fig.tight_layout()
            self.canvas.draw()
            
            # Update status with appropriate parameters
            if self.use_reference_mode.get():
                target_mean = (self.target_max_load.get()/self.ref_pos_load.get() + self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                target_amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                status_msg = f"Preview updated: {waveform_type} waveform with target mean={target_mean:.2f}, amplitude={target_amplitude:.2f}"
            else:
                status_msg = f"Preview updated: {waveform_type} waveform with mean={self.mean_load.get()}, amplitude={self.load_amplitude.get()}"
                
            if self.enable_stochastic.get():
                status_msg += f", {self.distribution_type.get()} distribution"
            self.status_var.set(status_msg)
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to update preview: {str(e)}")
            self.status_var.set("Error updating preview")
    
    def browse_output_file(self):
        """Open a file dialog to select the output file"""
        # Determine file extension based on selected format
        extension = ".xlsx" if self.export_format.get() == "excel" else ".mat"
        filetypes = [("Excel files", "*.xlsx"), ("MATLAB files", "*.mat"), ("All files", "*.*")] if extension == ".xlsx" else [("MATLAB files", "*.mat"), ("Excel files", "*.xlsx"), ("All files", "*.*")]
        
        # Update default filename if needed
        current_file = self.output_file.get()
        if not current_file.endswith(extension):
            current_file = os.path.splitext(current_file)[0] + extension
        
        filename = filedialog.asksaveasfilename(
            defaultextension=extension,
            filetypes=filetypes,
            initialfile=current_file
        )
        if filename:
            self.output_file.set(filename)
            self.status_var.set(f"Output file set to: {filename}")
    
    def export_data(self):
        """Export data in the selected format"""
        export_format = self.export_format.get()
        
        if export_format == "excel":
            if self.sequential_scenarios:  # Check directly for sequential scenarios
                self.export_sequential_to_excel()
            elif self.loading_scenarios:
                self.export_superposed_to_excel()
            else:
                self.export_to_excel()
        elif export_format == "matlab":
            if self.sequential_scenarios:  # Check directly for sequential scenarios
                self.export_sequential_to_matlab()
            elif self.loading_scenarios:
                self.export_superposed_to_matlab()
            else:
                self.export_to_matlab()
        else:
            messagebox.showerror("Export Error", "Invalid export format selected")
    
    def export_to_matlab(self):
        """Export the current configuration to a MATLAB .mat file for FatLab"""
        try:
            # Get the output file path
            output_file = self.output_file.get()
            if not output_file.endswith('.mat'):
                output_file = os.path.splitext(output_file)[0] + '.mat'
            
            # Check if we have superposed scenarios
            if self.loading_scenarios:
                # Export superposed scenarios
                self.export_superposed_to_matlab(output_file)
            else:
                # Export single scenario
                # Get the parameters
                num_rep = self.num_repetitions.get()
                description = self.export_description.get()
                
                # Generate time and load data using the generate_waveform method
                t, y = self.generate_waveform()
                
                if t is None or y is None:
                    return
                
                # Determine mean and amplitude values based on mode
                if self.use_reference_mode.get():
                    # Use the updated formula for reference/target mode
                    mean = (self.target_max_load.get()/self.ref_pos_load.get() + self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                    amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                else:
                    # Use direct values for standard mode
                    mean = self.mean_load.get()
                    amplitude = self.load_amplitude.get()
                
                # Create the load_mat matrix (time in first column, load in second column)
                load_mat = np.column_stack((t, y))
                
                # Create the component names and units
                component_names = np.array(['Fx_1'])
                component_units = np.array(['kN'])
                
                # Create dictionary for savemat
                matlab_dict = {
                    'load_mat': load_mat,
                    'component_names': component_names,
                    'component_units': component_units,
                    'num_repetitions': num_rep,
                    'description': description,
                    'mean': mean,
                    'amplitude': amplitude
                }
                
                # Save to .mat file
                scipy.io.savemat(output_file, matlab_dict)
                
                messagebox.showinfo("Export Successful", f"Data exported to {output_file}")
                self.status_var.set(f"Data exported to {output_file}")
        
        except Exception as e:
            messagebox.showerror("Export Error", f"Failed to export data: {str(e)}")
            self.status_var.set("Error exporting data")
    
    def export_superposed_to_matlab(self, output_file):
        """Export superposed loading scenarios to a MATLAB .mat file"""
        try:
            # Get export parameters
            num_rep = self.num_repetitions.get()
            description = self.export_description.get()
            
            # Generate time points that cover all scenarios
            # Find the lowest frequency to determine the longest period
            min_freq = min([scenario['frequency'] for scenario in self.loading_scenarios])
            max_freq = max([scenario['frequency'] for scenario in self.loading_scenarios])
            
            # Use a time span that shows at least 2 cycles of the lowest frequency
            # and enough resolution for the highest frequency
            t = np.linspace(0, 2/min_freq, int(1000 * max_freq/min_freq))
            
            # Initialize the superposed waveform
            y_superposed = np.zeros_like(t)
            
            # Add each scenario's waveform
            for scenario in self.loading_scenarios:
                mean = scenario['mean_load']
                amplitude = scenario['load_amplitude']
                waveform = scenario['waveform_type']
                frequency = scenario['frequency']
                phase_shift_deg = scenario['phase_shift']
                phase_shift_rad = np.deg2rad(phase_shift_deg)
                
                # Generate the waveform
                if waveform == "Sinusoidal":
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                elif waveform == "Triangular":
                    y = mean + amplitude * (2 * np.abs(2 * (frequency * t - np.floor(frequency * t + 0.5))) - 1)
                elif waveform == "Square":
                    y = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * t + phase_shift_rad))
                else:  # Custom or fallback
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                
                # Apply stochastic aspects if enabled for this scenario
                if scenario['enable_stochastic']:
                    # Get stochastic parameters
                    distribution = scenario['distribution_type']
                    param1 = scenario['distribution_param1']
                    param2 = scenario['distribution_param2']
                    noise_level = scenario['noise_level'] / 100.0  # Convert from percentage
                    
                    # Generate random noise based on the selected distribution
                    if distribution == "Gaussian":
                        noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t))
                    elif distribution == "Weibull":
                        noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t))
                    elif distribution == "Log-normal":
                        noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t))
                    elif distribution == "Exponential":
                        noise = stats.expon.rvs(scale=param1, size=len(t))
                    
                    # Scale the noise to the desired level relative to the amplitude
                    scaled_noise = noise * amplitude * noise_level
                    
                    # Add the noise to the waveform
                    y = y + scaled_noise
                
                # Add to the superposed waveform
                y_superposed += y
            
            # Create the load_mat matrix (time in first column, load in second column)
            load_mat = np.column_stack((t, y_superposed))
            
            # Create the component names and units
            comp = np.array([["Load", "N"]], dtype=object)
            
            # Calculate total cycles as the sum of all scenarios' cycles
            total_cycles = sum([scenario['total_cycles'] for scenario in self.loading_scenarios])
            
            # Create the out_strct structure as expected by FatLab
            out_strct = {
                'desc': description + " (Superposed)",
                'num_rep': num_rep,
                'num_comp': 1,  # Number of load components (just one for now)
                'num_time': len(t),  # Number of time steps
                'comp': comp,
                'load_mat': load_mat,
                'total_cycles': total_cycles,  # Add total cycles
                'scenarios': self.loading_scenarios  # Store the individual scenarios for reference
            }
            
            # Save to a .mat file
            sio.savemat(output_file, {'out_strct': out_strct})
            
        except Exception as e:
            raise Exception(f"Failed to export superposed data: {str(e)}")
    
    def reset_form(self):
        """Reset the form to default values"""
        self.mean_load.set(0.0)
        self.load_amplitude.set(1.0)
        self.load_ratio.set(0.1)
        self.waveform_type.set("Sinusoidal")
        self.frequency.set(1.0)
        self.phase_shift.set(0.0)
        self.total_cycles.set(1000)
        
        # Reset stochastic parameters
        self.enable_stochastic.set(False)
        self.distribution_type.set("Gaussian")
        self.distribution_param1.set(0.0)
        self.distribution_param2.set(1.0)
        self.noise_level.set(0.1)
        
        # Clear loading scenarios
        self.loading_scenarios.clear()
        self.update_scenario_listbox()
        
        # Clear sequential scenarios
        self.sequential_scenarios.clear()
        self.update_sequential_listbox()
        
        # Reset transition options
        self.transition_type.set("Linear")
        self.transition_cycles.set(100)
        self.match_phase.set(True)
        self.is_sequential_mode.set(False)
        
        self.update_preview()
        self.status_var.set("Form reset to default values")
    
    def load_configuration(self):
        """Load a configuration from a JSON file"""
        try:
            # Get the file path
            file_path = filedialog.askopenfilename(
                filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
                title="Load Configuration"
            )
            
            if not file_path:
                return  # User cancelled
            
            # Load from file
            with open(file_path, 'r') as f:
                config = json.load(f)
            
            # Apply the configuration parameters
            # General settings
            self.mean_load.set(config.get("mean_load", 0))
            self.load_amplitude.set(config.get("load_amplitude", 0))
            self.load_ratio.set(config.get("load_ratio", 0))
            self.waveform_type.set(config.get("waveform_type", "Sinusoidal"))
            self.frequency.set(config.get("frequency", 1))
            self.phase_shift.set(config.get("phase_shift", 0))
            self.total_cycles.set(config.get("total_cycles", 1000))
            
            # Stochastic settings
            self.enable_stochastic.set(config.get("enable_stochastic", False))
            self.distribution_type.set(config.get("distribution_type", "Gaussian"))
            self.distribution_param1.set(config.get("distribution_param1", 0))
            self.distribution_param2.set(config.get("distribution_param2", 1))
            self.noise_level.set(config.get("noise_level", 5))
            
            # Export settings
            self.output_file.set(config.get("output_file", ""))
            self.export_description.set(config.get("export_description", "Fatigue Test"))
            self.num_repetitions.set(config.get("num_repetitions", 1))
            
            # Superposition settings
            self.is_superposition_mode.set(config.get("is_superposition_mode", False))
            
            # Load scenarios if available
            if "loading_scenarios" in config:
                self.loading_scenarios = config["loading_scenarios"]
                # Update the listbox
                self.update_scenario_listbox()
            
            # Sequential loading settings
            self.is_sequential_mode.set(config.get("is_sequential_mode", False))
            
            # Load sequential scenarios if available
            if "sequential_scenarios" in config:
                self.sequential_scenarios = config["sequential_scenarios"]
                # Update the sequential listbox
                self.update_sequential_listbox()
            
            # Load transition settings
            self.transition_type.set(config.get("transition_type", "Linear"))
            self.transition_cycles.set(config.get("transition_cycles", 10))
            self.match_phase.set(config.get("match_phase", True))
            
            # Update the UI based on the loaded configuration
            self.update_ui_from_config()
            
            messagebox.showinfo("Load Configuration", "Configuration loaded successfully")
            self.status_var.set(f"Configuration loaded from {file_path}")
            
        except Exception as e:
            messagebox.showerror("Load Error", f"Failed to load configuration: {str(e)}")
            self.status_var.set("Error loading configuration")
    
    def save_configuration(self):
        """Save the current configuration to a JSON file"""
        try:
            # Get the file path
            file_path = filedialog.asksaveasfilename(
                defaultextension=".json",
                filetypes=[("JSON files", "*.json"), ("All files", "*.*")],
                title="Save Configuration"
            )
            
            if not file_path:
                return  # User cancelled
            
            # Create a dictionary with all the configuration parameters
            config = {
                # General settings
                "mean_load": self.mean_load.get(),
                "load_amplitude": self.load_amplitude.get(),
                "load_ratio": self.load_ratio.get(),
                "waveform_type": self.waveform_type.get(),
                "frequency": self.frequency.get(),
                "phase_shift": self.phase_shift.get(),
                "total_cycles": self.total_cycles.get(),
                
                # Stochastic settings
                "enable_stochastic": self.enable_stochastic.get(),
                "distribution_type": self.distribution_type.get(),
                "distribution_param1": self.distribution_param1.get(),
                "distribution_param2": self.distribution_param2.get(),
                "noise_level": self.noise_level.get(),
                
                # Export settings
                "output_file": self.output_file.get(),
                "export_description": self.export_description.get(),
                "num_repetitions": self.num_repetitions.get(),
                
                # Superposition settings
                "is_superposition_mode": self.is_superposition_mode.get(),
                "loading_scenarios": self.loading_scenarios,
                
                # Sequential loading settings
                "is_sequential_mode": self.is_sequential_mode.get(),
                "sequential_scenarios": self.sequential_scenarios,
                "transition_type": self.transition_type.get(),
                "transition_cycles": self.transition_cycles.get(),
                "match_phase": self.match_phase.get()
            }
            
            # Save to file
            with open(file_path, 'w') as f:
                json.dump(config, f, indent=4)
            
            messagebox.showinfo("Save Configuration", "Configuration saved successfully")
            self.status_var.set(f"Configuration saved to {file_path}")
            
        except Exception as e:
            messagebox.showerror("Save Error", f"Failed to save configuration: {str(e)}")
            self.status_var.set("Error saving configuration")
    
    def show_about(self):
        """Show the about dialog"""
        about_text = """Fatigue Setup Generator

A Windows 95 style graphical user interface for generating 
fatigue loading scenarios for durability simulations in FatLab.

Version 1.0
"""
        messagebox.showinfo("About Fatigue Setup Generator", about_text)
    
    def load_demo_data(self):
        """Load demo data for screenshot or demonstration purposes"""
        # Clear any existing scenarios
        self.loading_scenarios.clear()
        self.sequential_scenarios.clear()
        
        # Set up the first scenario (current form)
        self.mean_load.set(10.0)
        self.load_amplitude.set(5.0)
        self.load_ratio.set(0.2)
        self.waveform_type.set("Sinusoidal")
        self.frequency.set(1.0)
        self.phase_shift.set(0.0)
        self.total_cycles.set(1000)
        
        # Demo stochastic data
        self.enable_stochastic.set(True)
        self.distribution_type.set("Gaussian")
        self.distribution_param1.set(0.0)
        self.distribution_param2.set(0.5)
        self.noise_level.set(10.0)  # 10%
        
        # Add the current setup as the first scenario for both superposition and sequential
        self.add_current_scenario()
        self.add_sequential_scenario()
        
        # Create a second scenario with different parameters
        scenario2 = {
            'mean_load': 5.0,
            'load_amplitude': 2.5,
            'load_ratio': 0.1,
            'waveform_type': "Sinusoidal",  # Changed to Sinusoidal for better phase matching demo
            'frequency': 2.0,
            'phase_shift': 45.0,
            'total_cycles': 2000,
            'enable_stochastic': False,
            'distribution_type': "Gaussian",
            'distribution_param1': 0.0,
            'distribution_param2': 1.0,
            'noise_level': 0.0
        }
        
        # Add the second scenario to both lists
        self.loading_scenarios.append(scenario2)
        self.sequential_scenarios.append(scenario2)
        
        # Create a third scenario with different parameters
        scenario3 = {
            'mean_load': 2.0,
            'load_amplitude': 1.0,
            'load_ratio': 0.05,
            'waveform_type': "Sinusoidal",  # Changed to Sinusoidal for better phase matching demo
            'frequency': 0.5,
            'phase_shift': 90.0,
            'total_cycles': 500,
            'enable_stochastic': False,
            'distribution_type': "Gaussian",
            'distribution_param1': 0.0,
            'distribution_param2': 1.0,
            'noise_level': 0.0
        }
        
        # Add the third scenario to both lists
        self.loading_scenarios.append(scenario3)
        self.sequential_scenarios.append(scenario3)
        
        # Update the scenario listboxes
        self.update_scenario_listbox()
        self.update_sequential_listbox()
        
        # Set transition options for sequential loading
        self.transition_type.set("Phase-Matched")  # Use the new Phase-Matched transition type
        self.transition_cycles.set(50)
        self.match_phase.set(True)  # Enable phase matching
        
        # Randomly choose between previewing superposition or sequential loading
        if random.choice([True, False]):
            # Preview the superposition
            self.preview_superposition()
            self.is_superposition_mode.set(True)
            self.is_sequential_mode.set(False)
            self.status_var.set("Demo data loaded with 3 superposed loading scenarios")
        else:
            # Preview the sequential loading
            self.preview_sequential()
            self.is_superposition_mode.set(False)
            self.is_sequential_mode.set(True)
            self.status_var.set("Demo data loaded with 3 sequential loading scenarios and phase-matched transitions")
    
    def export_data_with_format(self, format_type):
        """Set the export format and open the export dialog"""
        self.export_format.set(format_type)
        self.notebook.select(self.export_tab)

    def export_to_excel(self):
        """Export the current configuration to an Excel file"""
        try:
            # Get the output file path
            output_file = self.output_file.get()
            if not output_file.endswith('.xlsx'):
                output_file = os.path.splitext(output_file)[0] + '.xlsx'
            
            # Get common parameters
            waveform_type = self.waveform_type.get()
            frequency = self.frequency.get()
            phase_shift = self.phase_shift.get()
            num_rep = self.num_repetitions.get()
            total_cycles = self.total_cycles.get()
            
            # Generate time and load data using the generate_waveform method
            t, y = self.generate_waveform()
            
            if t is None or y is None:
                return
            
            # Reduce data density if needed to fit Excel limits
            max_points = 1000000  # Safe limit below Excel's maximum
            if len(t) > max_points:
                # Calculate step size to reduce points
                step = len(t) // max_points
                t = t[::step]
                y = y[::step]
            
            # Calculate period from frequency
            period = 1.0 / frequency if frequency > 0 else 0
            
            # Create a workbook and add a worksheet
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.title = "LC1"  # Name the sheet LC1
            
            # Set the number of repetitions in cell B1
            ws['A1'] = "n"
            ws['B1'] = num_rep
            
            # Set the column headers in row 2
            ws['A2'] = "Time"
            ws['B2'] = "Fx"
            ws['G2'] = "amplitude"
            ws['H2'] = "mean"
            ws['I2'] = "phase"
            ws['J2'] = "Period"
            ws['K2'] = "frequency"
            ws['L2'] = "cycles"  # Add cycles column
            
            # Determine mean and amplitude values based on mode
            if self.use_reference_mode.get():
                # Use the updated formula for reference/target mode
                mean = (self.target_max_load.get()/self.ref_pos_load.get() + self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
            else:
                # Use direct values for standard mode
                mean = self.mean_load.get()
                amplitude = self.load_amplitude.get()
            
            # Set the units in row 3
            ws['A3'] = "s"
            ws['B3'] = "kN"
            ws['G3'] = "Fx"
            ws['H3'] = amplitude
            ws['I3'] = mean
            ws['J3'] = phase_shift
            ws['K3'] = period
            ws['L3'] = frequency
            ws['M3'] = total_cycles  # Add total cycles
            
            # Add time and load data starting from row 4
            for i, (time_val, load_val) in enumerate(zip(t, y), 4):  # Start from row 4
                cell = ws[f'A{i}']
                cell.value = time_val
                cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                
                cell = ws[f'B{i}']
                cell.value = load_val
                cell.number_format = '0.000000'  # Set number format to show 6 decimal places
            
            # Save the workbook
            wb.save(output_file)
            
            messagebox.showinfo("Export Successful", f"Data exported to {output_file}")
            self.status_var.set(f"Data exported to {output_file}")
            
        except Exception as e:
            messagebox.showerror("Export Error", f"Failed to export data: {str(e)}")
            self.status_var.set("Error exporting data")
    
    def export_superposed_to_excel(self, output_file):
        """Export superposed loading scenarios to an Excel file"""
        try:
            # Get export parameters
            num_rep = self.num_repetitions.get()
            
            # Generate time points that cover all scenarios
            # Find the lowest frequency to determine the longest period
            min_freq = min([scenario['frequency'] for scenario in self.loading_scenarios])
            max_freq = max([scenario['frequency'] for scenario in self.loading_scenarios])
            
            # Use a time span that shows at least 2 cycles of the lowest frequency
            # and enough resolution for the highest frequency
            t = np.linspace(0, 2/min_freq, int(1000 * max_freq/min_freq))
            
            # Initialize the superposed waveform
            y_superposed = np.zeros_like(t)
            
            # Add each scenario's waveform
            for scenario in self.loading_scenarios:
                mean = scenario['mean_load']
                amplitude = scenario['load_amplitude']
                waveform = scenario['waveform_type']
                frequency = scenario['frequency']
                phase_shift_deg = scenario['phase_shift']
                phase_shift_rad = np.deg2rad(phase_shift_deg)
                
                # Generate the waveform
                if waveform == "Sinusoidal":
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                elif waveform == "Triangular":
                    y = mean + amplitude * (2 * np.abs(2 * (frequency * t - np.floor(frequency * t + 0.5))) - 1)
                elif waveform == "Square":
                    y = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * t + phase_shift_rad))
                else:  # Custom or fallback
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                
                # Apply stochastic aspects if enabled for this scenario
                if scenario['enable_stochastic']:
                    # Get stochastic parameters
                    distribution = scenario['distribution_type']
                    param1 = scenario['distribution_param1']
                    param2 = scenario['distribution_param2']
                    noise_level = scenario['noise_level'] / 100.0  # Convert from percentage
                    
                    # Generate random noise based on the selected distribution
                    if distribution == "Gaussian":
                        noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t))
                    elif distribution == "Weibull":
                        noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t))
                    elif distribution == "Log-normal":
                        noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t))
                    elif distribution == "Exponential":
                        noise = stats.expon.rvs(scale=param1, size=len(t))
                    
                    # Scale the noise to the desired level relative to the amplitude
                    scaled_noise = noise * amplitude * noise_level
                    
                    # Add the noise to the waveform
                    y = y + scaled_noise
                
                # Add to the superposed waveform
                y_superposed += y
            
            # Calculate total cycles as the sum of all scenarios' cycles
            total_cycles = sum([scenario['total_cycles'] for scenario in self.loading_scenarios])
            
            # Calculate effective period and frequency for the superposed waveform
            # Use the lowest frequency as the effective frequency
            effective_frequency = min_freq
            effective_period = 1.0 / effective_frequency if effective_frequency > 0 else 0
            
            # Create a workbook and add a worksheet
            wb = openpyxl.Workbook()
            ws = wb.active
            ws.title = "LC1"  # Name the sheet LC1
            
            # Set the number of repetitions in cell B1
            ws['A1'] = "n"
            ws['B1'] = num_rep
            
            # Set the column headers in row 2
            ws['A2'] = "Time"
            ws['B2'] = "Fx"
            ws['G2'] = "amplitude"
            ws['H2'] = "mean"
            ws['I2'] = "phase"
            ws['J2'] = "Period"
            ws['K2'] = "frequency"
            ws['L2'] = "cycles"  # Add cycles column
            
            # Calculate effective amplitude and mean for the superposed waveform
            effective_mean = np.mean(y_superposed)
            effective_amplitude = (np.max(y_superposed) - np.min(y_superposed)) / 2
            
            # Set the units in row 3
            ws['A3'] = "s"
            ws['B3'] = "kN"
            ws['G3'] = "Fx"
            ws['H3'] = effective_amplitude
            ws['I3'] = effective_mean
            ws['J3'] = 0  # Phase shift not applicable for superposed
            ws['K3'] = effective_period
            ws['L3'] = effective_frequency
            ws['M3'] = total_cycles  # Add total cycles
            
            # Add time and load data starting from row 4
            for i, (time_val, load_val) in enumerate(zip(t, y_superposed), 4):  # Start from row 4
                cell = ws[f'A{i}']
                cell.value = time_val
                cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                
                cell = ws[f'B{i}']
                cell.value = load_val
                cell.number_format = '0.000000'  # Set number format to show 6 decimal places
            
            # Add a second sheet with information about the individual scenarios
            ws_info = wb.create_sheet(title="Scenarios")
            
            # Add headers
            headers = ["Scenario", "Waveform", "Mean", "Amplitude", "Frequency", "Phase", "Cycles"]
            for col, header in enumerate(headers, 1):
                ws_info.cell(row=1, column=col, value=header)
            
            # Add scenario information
            for row, scenario in enumerate(self.loading_scenarios, 2):
                ws_info.cell(row=row, column=1, value=f"Scenario {row-1}")
                ws_info.cell(row=row, column=2, value=scenario['waveform_type'])
                ws_info.cell(row=row, column=3, value=scenario['mean_load'])
                ws_info.cell(row=row, column=4, value=scenario['load_amplitude'])
                ws_info.cell(row=row, column=5, value=scenario['frequency'])
                ws_info.cell(row=row, column=6, value=scenario['phase_shift'])
                ws_info.cell(row=row, column=7, value=scenario['total_cycles'])
            
            # Save the workbook
            wb.save(output_file)
            
        except Exception as e:
            raise Exception(f"Failed to export superposed data: {str(e)}")

    def create_superposition_form(self):
        """Create the superposition form for managing multiple loading scenarios"""
        # Create a frame for the form
        superposition_frame = ttk.LabelFrame(self.superposition_tab, text="Loading Scenarios", style="Win95.TLabelframe")
        superposition_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Create a frame for the list of scenarios
        list_frame = ttk.Frame(superposition_frame, style="Win95.TFrame")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Create a listbox to display the scenarios
        self.scenario_listbox = tk.Listbox(list_frame, height=10, width=50)
        self.scenario_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        
        # Add a scrollbar to the listbox
        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.scenario_listbox.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.scenario_listbox.config(yscrollcommand=scrollbar.set)
        
        # Create a frame for the buttons
        button_frame = ttk.Frame(superposition_frame, style="Win95.TFrame")
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # Add buttons for managing scenarios
        ttk.Button(button_frame, text="Add Current Setup", command=self.add_current_scenario, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Remove Selected", command=self.remove_selected_scenario, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Clear All", command=self.clear_all_scenarios, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Preview Superposition", command=self.preview_superposition, 
                  style="Win95.TButton").pack(side=tk.RIGHT, padx=5)
    
    def add_current_scenario(self):
        """Add the current setup as a new loading scenario"""
        # Create a dictionary with the current setup
        scenario = {
            'mean_load': self.mean_load.get(),
            'load_amplitude': self.load_amplitude.get(),
            'load_ratio': self.load_ratio.get(),
            'waveform_type': self.waveform_type.get(),
            'frequency': self.frequency.get(),
            'phase_shift': self.phase_shift.get(),
            'total_cycles': self.total_cycles.get(),
            'enable_stochastic': self.enable_stochastic.get(),
            'distribution_type': self.distribution_type.get(),
            'distribution_param1': self.distribution_param1.get(),
            'distribution_param2': self.distribution_param2.get(),
            'noise_level': self.noise_level.get()
        }
        
        # Add the scenario to the list
        self.loading_scenarios.append(scenario)
        
        # Update the listbox
        self.update_scenario_listbox()
        
        # Update status
        self.status_var.set(f"Added scenario #{len(self.loading_scenarios)}: {self.waveform_type.get()} with mean={self.mean_load.get()}, amplitude={self.load_amplitude.get()}")
    
    def remove_selected_scenario(self):
        """Remove the selected scenario from the list"""
        # Get the selected index
        selected_index = self.scenario_listbox.curselection()
        
        if selected_index:
            # Remove the scenario
            index = selected_index[0]
            self.loading_scenarios.pop(index)
            
            # Update the listbox
            self.update_scenario_listbox()
            
            # Update status
            self.status_var.set(f"Removed scenario #{index + 1}")
        else:
            messagebox.showinfo("Selection Required", "Please select a scenario to remove")
    
    def clear_all_scenarios(self):
        """Clear all loading scenarios"""
        # Confirm with the user
        if messagebox.askyesno("Confirm Clear", "Are you sure you want to clear all scenarios?"):
            # Clear the list
            self.loading_scenarios.clear()
            
            # Update the listbox
            self.update_scenario_listbox()
            
            # Update status
            self.status_var.set("All scenarios cleared")
    
    def update_scenario_listbox(self):
        """Update the scenario listbox with the current list of scenarios"""
        # Clear the listbox
        self.scenario_listbox.delete(0, tk.END)
        
        # Add each scenario to the listbox
        for i, scenario in enumerate(self.loading_scenarios):
            waveform = scenario['waveform_type']
            mean = scenario['mean_load']
            amplitude = scenario['load_amplitude']
            frequency = scenario['frequency']
            cycles = scenario['total_cycles']
            
            # Create a description string
            description = f"#{i+1}: {waveform}, Mean={mean:.2f}, Amp={amplitude:.2f}, Freq={frequency:.2f}Hz, Cycles={cycles}"
            
            # Add to the listbox
            self.scenario_listbox.insert(tk.END, description)
    
    def preview_superposition(self):
        """Preview the superposition of all loading scenarios"""
        if not self.loading_scenarios:
            messagebox.showinfo("No Scenarios", "Please add at least one loading scenario")
            return
        
        try:
            # Clear the previous plot
            self.ax.clear()
            
            # Generate time points that cover all scenarios
            # Find the lowest frequency to determine the longest period
            min_freq = min([scenario['frequency'] for scenario in self.loading_scenarios])
            max_freq = max([scenario['frequency'] for scenario in self.loading_scenarios])
            
            # Use a time span that shows at least 2 cycles of the lowest frequency
            # and enough resolution for the highest frequency
            t = np.linspace(0, 2/min_freq, int(1000 * max_freq/min_freq))
            
            # Initialize the superposed waveform
            y_superposed = np.zeros_like(t)
            
            # Add each scenario's waveform
            for i, scenario in enumerate(self.loading_scenarios):
                mean = scenario['mean_load']
                amplitude = scenario['load_amplitude']
                waveform = scenario['waveform_type']
                frequency = scenario['frequency']
                phase_shift_deg = scenario['phase_shift']
                phase_shift_rad = np.deg2rad(phase_shift_deg)
                
                # Generate the waveform
                if waveform == "Sinusoidal":
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                elif waveform == "Triangular":
                    y = mean + amplitude * (2 * np.abs(2 * (frequency * t - np.floor(frequency * t + 0.5))) - 1)
                elif waveform == "Square":
                    y = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * t + phase_shift_rad))
                else:  # Custom or fallback
                    y = mean + amplitude * np.sin(2 * np.pi * frequency * t + phase_shift_rad)
                
                # Apply stochastic aspects if enabled for this scenario
                if scenario['enable_stochastic']:
                    # Get stochastic parameters
                    distribution = scenario['distribution_type']
                    param1 = scenario['distribution_param1']
                    param2 = scenario['distribution_param2']
                    noise_level = scenario['noise_level'] / 100.0  # Convert from percentage
                    
                    # Generate random noise based on the selected distribution
                    if distribution == "Gaussian":
                        noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t))
                    elif distribution == "Weibull":
                        noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t))
                    elif distribution == "Log-normal":
                        noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t))
                    elif distribution == "Exponential":
                        noise = stats.expon.rvs(scale=param1, size=len(t))
                    
                    # Scale the noise to the desired level relative to the amplitude
                    scaled_noise = noise * amplitude * noise_level
                    
                    # Add the noise to the waveform
                    y = y + scaled_noise
                
                # Plot this scenario's waveform with some transparency
                self.ax.plot(t, y, alpha=0.3, label=f"Scenario #{i+1}")
                
                # Add to the superposed waveform
                y_superposed += y
            
            # Plot the superposed waveform
            self.ax.plot(t, y_superposed, 'k-', linewidth=2, label='Superposed')
            
            # Set labels and title
            self.ax.set_xlabel('Time (s)')
            self.ax.set_ylabel('Load')
            self.ax.set_title(f'Superposition of {len(self.loading_scenarios)} Loading Scenarios')
            
            # Add grid and legend
            self.ax.grid(True)
            self.ax.legend()
            
            # Update the canvas
            self.fig.tight_layout()
            self.canvas.draw()
            
            # Update status
            self.status_var.set(f"Previewing superposition of {len(self.loading_scenarios)} scenarios")
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to preview superposition: {str(e)}")
            self.status_var.set("Error previewing superposition")

    def create_sequential_form(self):
        """Create the sequential loading form for managing sequential loading scenarios with transitions"""
        # Create a frame for the form
        sequential_frame = ttk.LabelFrame(self.sequential_tab, text="Sequential Loading Scenarios", style="Win95.TLabelframe")
        sequential_frame.pack(fill=tk.BOTH, expand=True, padx=10, pady=10)
        
        # Create a frame for the list of scenarios
        list_frame = ttk.Frame(sequential_frame, style="Win95.TFrame")
        list_frame.pack(fill=tk.BOTH, expand=True, padx=5, pady=5)
        
        # Create a listbox to display the scenarios
        self.sequential_listbox = tk.Listbox(list_frame, height=10, width=50)
        self.sequential_listbox.pack(side=tk.LEFT, fill=tk.BOTH, expand=True)
        
        # Add a scrollbar to the listbox
        scrollbar = ttk.Scrollbar(list_frame, orient=tk.VERTICAL, command=self.sequential_listbox.yview)
        scrollbar.pack(side=tk.RIGHT, fill=tk.Y)
        self.sequential_listbox.config(yscrollcommand=scrollbar.set)
        
        # Create a frame for transition options
        transition_frame = ttk.LabelFrame(sequential_frame, text="Transition Options", style="Win95.TLabelframe")
        transition_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # Transition type
        ttk.Label(transition_frame, text="Transition Type:", style="Win95.TLabel").grid(
            row=0, column=0, sticky=tk.W, padx=5, pady=5)
        transition_combo = ttk.Combobox(transition_frame, textvariable=self.transition_type, style="Win95.TCombobox")
        transition_combo['values'] = ('Linear', 'Smooth', 'Step', 'None', 'Phase-Matched')
        transition_combo.grid(row=0, column=1, sticky=tk.W, padx=5, pady=5)
        
        # Add event handler for transition type changes
        transition_combo.bind("<<ComboboxSelected>>", self.on_transition_type_changed)
        
        # Transition cycles
        ttk.Label(transition_frame, text="Transition Cycles:", style="Win95.TLabel").grid(
            row=1, column=0, sticky=tk.W, padx=5, pady=5)
        ttk.Entry(transition_frame, textvariable=self.transition_cycles, width=10, style="Win95.TEntry").grid(
            row=1, column=1, sticky=tk.W, padx=5, pady=5)
        
        # Phase matching option
        self.match_phase = tk.BooleanVar(value=True)
        self.match_phase_checkbox = ttk.Checkbutton(transition_frame, text="Match Phase at Transitions", variable=self.match_phase, 
                       style="Win95.TCheckbutton")
        self.match_phase_checkbox.grid(row=2, column=0, columnspan=2, sticky=tk.W, padx=5, pady=5)
        
        # Export option
        self.separate_export = tk.BooleanVar(value=False)
        self.separate_export_checkbox = ttk.Checkbutton(transition_frame, text="Export Scenarios to Separate Sheets", 
                                                      variable=self.separate_export, style="Win95.TCheckbutton")
        self.separate_export_checkbox.grid(row=3, column=0, columnspan=2, sticky=tk.W, padx=5, pady=5)
        
        # Create a frame for the buttons
        button_frame = ttk.Frame(sequential_frame, style="Win95.TFrame")
        button_frame.pack(fill=tk.X, padx=5, pady=5)
        
        # Add buttons for managing scenarios
        ttk.Button(button_frame, text="Add Current Setup", command=self.add_sequential_scenario, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Remove Selected", command=self.remove_sequential_scenario, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Move Up", command=self.move_scenario_up, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Move Down", command=self.move_scenario_down, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Clear All", command=self.clear_sequential_scenarios, 
                  style="Win95.TButton").pack(side=tk.LEFT, padx=5)
        ttk.Button(button_frame, text="Preview Sequential", command=self.preview_sequential, 
                  style="Win95.TButton").pack(side=tk.RIGHT, padx=5)
    
    def add_sequential_scenario(self):
        """Add the current setup as a new sequential loading scenario"""
        # Create a dictionary with the current setup
        if self.use_reference_mode.get():
            # Calculate mean and amplitude based on reference/target mode
            mean = (self.target_max_load.get()/self.ref_pos_load.get() + self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
            amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
            
            scenario = {
                'mean_load': mean,
                'load_amplitude': amplitude,
                'load_ratio': self.load_ratio.get(),
                'waveform_type': self.waveform_type.get(),
                'frequency': self.frequency.get(),
                'phase_shift': self.phase_shift.get(),
                'total_cycles': self.total_cycles.get(),
                'enable_stochastic': self.enable_stochastic.get(),
                'distribution_type': self.distribution_type.get(),
                'distribution_param1': self.distribution_param1.get(),
                'distribution_param2': self.distribution_param2.get(),
                'noise_level': self.noise_level.get(),
                'is_reference_mode': True,
                'ref_pos_load': self.ref_pos_load.get(),
                'ref_neg_load': self.ref_neg_load.get(),
                'target_max_load': self.target_max_load.get(),
                'target_min_load': self.target_min_load.get()
            }
            
            # Update status message with reference/target mode information
            status_msg = f"Added sequential scenario #{len(self.sequential_scenarios)+1}: {self.waveform_type.get()} (Reference/Target Mode)"
            status_msg += f" with calculated mean={mean:.2f}, amplitude={amplitude:.2f}"
            
        else:
            # Standard mode - use direct values
            scenario = {
                'mean_load': self.mean_load.get(),
                'load_amplitude': self.load_amplitude.get(),
                'load_ratio': self.load_ratio.get(),
                'waveform_type': self.waveform_type.get(),
                'frequency': self.frequency.get(),
                'phase_shift': self.phase_shift.get(),
                'total_cycles': self.total_cycles.get(),
                'enable_stochastic': self.enable_stochastic.get(),
                'distribution_type': self.distribution_type.get(),
                'distribution_param1': self.distribution_param1.get(),
                'distribution_param2': self.distribution_param2.get(),
                'noise_level': self.noise_level.get(),
                'is_reference_mode': False
            }
            
            # Standard status message
            status_msg = f"Added sequential scenario #{len(self.sequential_scenarios)+1}: {self.waveform_type.get()}"
            status_msg += f" with mean={self.mean_load.get()}, amplitude={self.load_amplitude.get()}"
        
        # Add the scenario to the list
        self.sequential_scenarios.append(scenario)
        
        # Update the listbox
        self.update_sequential_listbox()
        
        # Update status
        self.status_var.set(status_msg)
    
    def remove_sequential_scenario(self):
        """Remove the selected scenario from the sequential list"""
        # Get the selected index
        selected_index = self.sequential_listbox.curselection()
        
        if selected_index:
            # Remove the scenario
            index = selected_index[0]
            self.sequential_scenarios.pop(index)
            
            # Update the listbox
            self.update_sequential_listbox()
            
            # Update status
            self.status_var.set(f"Removed sequential scenario #{index + 1}")
        else:
            messagebox.showinfo("Selection Required", "Please select a scenario to remove")
    
    def move_scenario_up(self):
        """Move the selected scenario up in the list"""
        # Get the selected index
        selected_index = self.sequential_listbox.curselection()
        
        if selected_index:
            index = selected_index[0]
            if index > 0:
                # Swap with the previous scenario
                self.sequential_scenarios[index], self.sequential_scenarios[index-1] = \
                    self.sequential_scenarios[index-1], self.sequential_scenarios[index]
                
                # Update the listbox
                self.update_sequential_listbox()
                
                # Select the moved item
                self.sequential_listbox.selection_set(index-1)
                
                # Update status
                self.status_var.set(f"Moved scenario #{index + 1} up")
        else:
            messagebox.showinfo("Selection Required", "Please select a scenario to move")
    
    def move_scenario_down(self):
        """Move the selected scenario down in the list"""
        # Get the selected index
        selected_index = self.sequential_listbox.curselection()
        
        if selected_index:
            index = selected_index[0]
            if index < len(self.sequential_scenarios) - 1:
                # Swap with the next scenario
                self.sequential_scenarios[index], self.sequential_scenarios[index+1] = \
                    self.sequential_scenarios[index+1], self.sequential_scenarios[index]
                
                # Update the listbox
                self.update_sequential_listbox()
                
                # Select the moved item
                self.sequential_listbox.selection_set(index+1)
                
                # Update status
                self.status_var.set(f"Moved scenario #{index + 1} down")
        else:
            messagebox.showinfo("Selection Required", "Please select a scenario to move")
    
    def clear_sequential_scenarios(self):
        """Clear all sequential loading scenarios"""
        # Confirm with the user
        if messagebox.askyesno("Confirm Clear", "Are you sure you want to clear all sequential scenarios?"):
            # Clear the list
            self.sequential_scenarios.clear()
            
            # Update the listbox
            self.update_sequential_listbox()
            
            # Update status
            self.status_var.set("All sequential scenarios cleared")
    
    def update_sequential_listbox(self):
        """Update the sequential scenario listbox with the current list of scenarios"""
        # Clear the listbox
        self.sequential_listbox.delete(0, tk.END)
        
        # Add each scenario to the listbox
        for i, scenario in enumerate(self.sequential_scenarios):
            waveform = scenario['waveform_type']
            mean = scenario['mean_load']
            amplitude = scenario['load_amplitude']
            frequency = scenario['frequency']
            cycles = scenario['total_cycles']
            
            # Create a description string
            if scenario.get('is_reference_mode', False):
                description = f"#{i+1}: {waveform} (Ref/Target), Mean={mean:.2f}, Amp={amplitude:.2f}, Freq={frequency:.2f}Hz, Cycles={cycles}"
            else:
                description = f"#{i+1}: {waveform}, Mean={mean:.2f}, Amp={amplitude:.2f}, Freq={frequency:.2f}Hz, Cycles={cycles}"
            
            # Add to the listbox
            self.sequential_listbox.insert(tk.END, description)
    
    def preview_sequential(self):
        """Preview the sequential loading scenarios with transitions"""
        if not self.sequential_scenarios:
            messagebox.showinfo("No Scenarios", "Please add at least one sequential loading scenario")
            return
        
        try:
            # Clear the previous plot
            self.ax.clear()
            
            # Get transition parameters
            transition_type = self.transition_type.get()
            transition_cycles = self.transition_cycles.get()
            match_phase = self.match_phase.get()
            
            # Generate the sequential waveform with transitions
            t_segments = []
            y_segments = []
            total_time = 0
            
            # Process each scenario
            for i, scenario in enumerate(self.sequential_scenarios):
                mean = scenario['mean_load']
                amplitude = scenario['load_amplitude']
                waveform = scenario['waveform_type']
                frequency = scenario['frequency']
                phase_shift_deg = scenario['phase_shift']
                
                # If phase matching is enabled and this is not the first scenario, calculate the phase shift
                # to match the phase at the transition point
                if match_phase and i > 0 and waveform in ["Sinusoidal", "Square"] and self.sequential_scenarios[i-1]['waveform_type'] in ["Sinusoidal", "Square"]:
                    # Get the last value of the previous segment
                    prev_segment = y_segments[i-1]
                    last_val = prev_segment[-1]
                    
                    # Calculate the phase shift needed to start this segment at the same value
                    # For sinusoidal waves: y = mean + amplitude * sin(2π*f*t + φ)
                    # We need to find φ such that sin(φ) = (last_val - mean) / amplitude
                    normalized_val = (last_val - mean) / amplitude
                    # Clamp to [-1, 1] to handle numerical errors
                    normalized_val = max(-1.0, min(1.0, normalized_val))
                    
                    # Calculate the phase shift in radians
                    if waveform == "Sinusoidal":
                        # For sinusoidal, we use arcsin
                        phase_shift_rad = np.arcsin(normalized_val)
                    else:  # Square
                        # For square wave, we use a simpler approach
                        phase_shift_rad = 0 if normalized_val >= 0 else np.pi
                    
                    # Convert to degrees for display
                    phase_shift_deg = np.rad2deg(phase_shift_rad)
                else:
                    # Use the original phase shift
                    phase_shift_rad = np.deg2rad(phase_shift_deg)
                
                # Calculate time for this segment
                cycles = scenario['total_cycles']
                segment_time = cycles / frequency
                
                # Generate time points for this segment
                # Increase resolution to at least 1000 points per second
                points_per_second = 1000
                min_points = int(segment_time * points_per_second)
                t_segment = np.linspace(total_time, total_time + segment_time, max(min_points, int(1000 * segment_time)))
                
                # Generate the waveform
                if waveform == "Sinusoidal":
                    y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                elif waveform == "Triangular":
                    y_segment = mean + amplitude * (2 * np.abs(2 * (frequency * (t_segment - total_time) - np.floor(frequency * (t_segment - total_time) + 0.5))) - 1)
                elif waveform == "Square":
                    y_segment = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad))
                else:  # Custom or fallback
                    y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                
                # Apply stochastic aspects if enabled for this scenario
                if scenario['enable_stochastic']:
                    # Get stochastic parameters
                    distribution = scenario['distribution_type']
                    param1 = scenario['distribution_param1']
                    param2 = scenario['distribution_param2']
                    noise_level = scenario['noise_level'] / 100.0  # Convert from percentage
                    
                    # Generate random noise based on the selected distribution
                    if distribution == "Gaussian":
                        noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t_segment))
                    elif distribution == "Weibull":
                        noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t_segment))
                    elif distribution == "Log-normal":
                        noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t_segment))
                    elif distribution == "Exponential":
                        noise = stats.expon.rvs(scale=param1, size=len(t_segment))
                    
                    # Scale the noise to the desired level relative to the amplitude
                    scaled_noise = noise * amplitude * noise_level
                    
                    # Add the noise to the waveform
                    y_segment = y_segment + scaled_noise
                
                # Add to the segments
                t_segments.append(t_segment)
                y_segments.append(y_segment)
                
                # Update total time
                total_time += segment_time
            
            # Apply transitions between segments
            t_combined = np.array([])
            y_combined = np.array([])
            
            for i in range(len(self.sequential_scenarios)):
                if i == 0:
                    # First segment, no transition needed at the beginning
                    t_combined = np.append(t_combined, t_segments[i])
                    y_combined = np.append(y_combined, y_segments[i])
                else:
                    # Apply transition between previous and current segment
                    prev_segment = y_segments[i-1]
                    curr_segment = y_segments[i]
                    
                    # Get the last value of the previous segment
                    last_val = prev_segment[-1]
                    
                    # Get the first value of the current segment
                    first_val = curr_segment[0]
                    
                    # Calculate transition time
                    transition_time = transition_cycles / self.sequential_scenarios[i]['frequency']
                    
                    # Generate transition time points - ensure no duplication with segment endpoints
                    if transition_type == "None":
                        # For "None" transition, just connect the segments without duplicating points
                        t_transition = np.array([])
                        y_transition = np.array([])
                    else:
                        # Ensure high resolution for transitions
                        transition_points = max(int(1000 * transition_time), 100)
                        t_transition = np.linspace(
                            t_segments[i-1][-1], 
                            t_segments[i][0], 
                            transition_points + 2
                        )[1:-1]  # Exclude endpoints to avoid duplicates
                    
                    # Generate transition values based on transition type
                    if transition_type == "Linear":
                        # Linear transition
                        y_transition = np.linspace(last_val, first_val, len(t_transition))
                    elif transition_type == "Smooth":
                        # Smooth (cubic) transition
                        x = np.linspace(0, 1, len(t_transition))
                        y_transition = last_val + (first_val - last_val) * (3*x**2 - 2*x**3)
                    elif transition_type == "Step":
                        # Step transition (abrupt change at midpoint)
                        y_transition = np.ones_like(t_transition) * last_val
                        mid_point = len(t_transition) // 2
                        y_transition[mid_point:] = first_val
                    elif transition_type == "Phase-Matched":
                        # Phase-matched transition - use a sinusoidal blend between the two segments
                        x = np.linspace(0, 1, len(t_transition))
                        # Use cosine blending for smooth transition
                        blend = 0.5 * (1 - np.cos(np.pi * x))
                        
                        # Get parameters for both segments
                        prev_scenario = self.sequential_scenarios[i-1]
                        curr_scenario = self.sequential_scenarios[i]
                        
                        # Calculate the waveform values for both segments at the transition points
                        if prev_scenario['waveform_type'] == "Sinusoidal" and curr_scenario['waveform_type'] == "Sinusoidal":
                            # For sinusoidal waves, we can create a smooth phase transition
                            prev_mean = prev_scenario['mean_load']
                            prev_amp = prev_scenario['load_amplitude']
                            prev_freq = prev_scenario['frequency']
                            
                            curr_mean = curr_scenario['mean_load']
                            curr_amp = curr_scenario['load_amplitude']
                            curr_freq = curr_scenario['frequency']
                            
                            # Calculate time relative to the start of each segment
                            t_rel_prev = t_transition - t_segments[i-1][0]
                            t_rel_curr = t_transition - t_segments[i][0]
                            
                            # Calculate phase at the transition point
                            prev_phase = 2 * np.pi * prev_freq * t_rel_prev[-1]
                            curr_phase = 0  # Start of the current segment
                            
                            # Generate waveforms for both segments at transition points
                            y_prev = prev_mean + prev_amp * np.sin(2 * np.pi * prev_freq * t_rel_prev + prev_phase)
                            y_curr = curr_mean + curr_amp * np.sin(2 * np.pi * curr_freq * t_rel_curr + curr_phase)
                            
                            # Blend the two waveforms
                            y_transition = (1 - blend) * y_prev + blend * y_curr
                        else:
                            # For other waveform types, use simple blending
                            y_transition = last_val + blend * (first_val - last_val)
                    
                    # Add transition to combined arrays if needed
                    if len(t_transition) > 0:
                        t_combined = np.append(t_combined, t_transition)
                        y_combined = np.append(y_combined, y_transition)
                    
                    # Add current segment (skip first point to avoid duplication)
                    t_combined = np.append(t_combined, t_segments[i][1:])
                    y_combined = np.append(y_combined, y_segments[i][1:])
            
            # Plot the sequential waveform
            self.ax.plot(t_combined, y_combined, 'b-', linewidth=1.5)
            
            # Add markers for the transitions
            for i in range(1, len(self.sequential_scenarios)):
                transition_point = t_segments[i][0]
                self.ax.axvline(x=transition_point, color='r', linestyle='--', alpha=0.5)
                self.ax.text(transition_point, np.max(y_combined), f"T{i}", 
                            color='r', ha='center', va='top', fontsize=8)
            
            # Set labels and title
            self.ax.set_xlabel('Time (s)')
            self.ax.set_ylabel('Load')
            transition_info = f"{transition_type} Transitions"
            if match_phase and transition_type != "Phase-Matched":
                transition_info += " with Phase Matching"
            elif transition_type == "Phase-Matched":
                # When using Phase-Matched transition type, always enable phase matching
                self.match_phase.set(True)
                match_phase = True
                transition_info = "Phase-Matched Transitions"
            self.ax.set_title(f'Sequential Loading with {transition_info}')
            
            # Add grid
            self.ax.grid(True)
            
            # Update the canvas
            self.fig.tight_layout()
            self.canvas.draw()
            
            # Update status
            phase_info = " with phase matching" if match_phase and transition_type != "Phase-Matched" else ""
            self.status_var.set(f"Previewing sequential loading with {len(self.sequential_scenarios)} scenarios and {transition_type} transitions{phase_info}")
            
            # Set sequential mode flag
            self.is_sequential_mode.set(True)
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to preview sequential loading: {str(e)}")
            self.status_var.set("Error previewing sequential loading")

    def export_sequential_to_matlab(self):
        """Export sequential loading scenarios to a MATLAB .mat file"""
        try:
            # Get the output file path
            output_file = self.output_file.get()
            if not output_file.endswith('.mat'):
                output_file = os.path.splitext(output_file)[0] + '.mat'
            
            # Get export parameters
            num_rep = self.num_repetitions.get()
            description = self.export_description.get()
            transition_type = self.transition_type.get()
            transition_cycles = self.transition_cycles.get()
            match_phase = self.match_phase.get()
            
            # Generate the sequential waveform with transitions
            t_segments = []
            y_segments = []
            total_time = 0
            
            # Process each scenario
            for i, scenario in enumerate(self.sequential_scenarios):
                mean = scenario['mean_load']
                amplitude = scenario['load_amplitude']
                waveform = scenario['waveform_type']
                frequency = scenario['frequency']
                phase_shift_deg = scenario['phase_shift']
                
                # If phase matching is enabled and this is not the first scenario, calculate the phase shift
                # to match the phase at the transition point
                if match_phase and i > 0 and waveform in ["Sinusoidal", "Square"] and self.sequential_scenarios[i-1]['waveform_type'] in ["Sinusoidal", "Square"]:
                    # Get the last value of the previous segment
                    prev_segment = y_segments[i-1]
                    last_val = prev_segment[-1]
                    
                    # Calculate the phase shift needed to start this segment at the same value
                    normalized_val = (last_val - mean) / amplitude
                    # Clamp to [-1, 1] to handle numerical errors
                    normalized_val = max(-1.0, min(1.0, normalized_val))
                    
                    # Calculate the phase shift in radians
                    if waveform == "Sinusoidal":
                        # For sinusoidal, we use arcsin
                        phase_shift_rad = np.arcsin(normalized_val)
                    else:  # Square
                        # For square wave, we use a simpler approach
                        phase_shift_rad = 0 if normalized_val >= 0 else np.pi
                    
                    # Convert to degrees for display
                    phase_shift_deg = np.rad2deg(phase_shift_rad)
                else:
                    # Use the original phase shift
                    phase_shift_rad = np.deg2rad(phase_shift_deg)
                
                # Calculate time for this segment
                cycles = scenario['total_cycles']
                segment_time = cycles / frequency
                
                # Generate time points for this segment
                t_segment = np.linspace(total_time, total_time + segment_time, int(1000 * segment_time))
                
                # Generate the waveform
                if waveform == "Sinusoidal":
                    y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                elif waveform == "Triangular":
                    y_segment = mean + amplitude * (2 * np.abs(2 * (frequency * (t_segment - total_time) - np.floor(frequency * (t_segment - total_time) + 0.5))) - 1)
                elif waveform == "Square":
                    y_segment = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad))
                else:  # Custom or fallback
                    y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                
                # Apply stochastic aspects if enabled for this scenario
                if scenario['enable_stochastic']:
                    # Get stochastic parameters
                    distribution = scenario['distribution_type']
                    param1 = scenario['distribution_param1']
                    param2 = scenario['distribution_param2']
                    noise_level = scenario['noise_level'] / 100.0  # Convert from percentage
                    
                    # Generate random noise based on the selected distribution
                    if distribution == "Gaussian":
                        noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t_segment))
                    elif distribution == "Weibull":
                        noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t_segment))
                    elif distribution == "Log-normal":
                        noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t_segment))
                    elif distribution == "Exponential":
                        noise = stats.expon.rvs(scale=param1, size=len(t_segment))
                    
                    # Scale the noise to the desired level relative to the amplitude
                    scaled_noise = noise * amplitude * noise_level
                    
                    # Add the noise to the waveform
                    y_segment = y_segment + scaled_noise
                
                # Add to the segments
                t_segments.append(t_segment)
                y_segments.append(y_segment)
                
                # Update total time
                total_time += segment_time
            
            # Apply transitions between segments
            t_combined = np.array([])
            y_combined = np.array([])
            
            for i in range(len(self.sequential_scenarios)):
                if i == 0:
                    # First segment, no transition needed at the beginning
                    t_combined = np.append(t_combined, t_segments[i])
                    y_combined = np.append(y_combined, y_segments[i])
                else:
                    # Apply transition between previous and current segment
                    prev_segment = y_segments[i-1]
                    curr_segment = y_segments[i]
                    
                    # Get the last value of the previous segment
                    last_val = prev_segment[-1]
                    
                    # Get the first value of the current segment
                    first_val = curr_segment[0]
                    
                    # Calculate transition time
                    transition_time = transition_cycles / self.sequential_scenarios[i]['frequency']
                    
                    # Generate transition time points - ensure no duplication with segment endpoints
                    if transition_type == "None":
                        # For "None" transition, just connect the segments without duplicating points
                        t_transition = np.array([])
                        y_transition = np.array([])
                    else:
                        # Ensure high resolution for transitions
                        transition_points = max(int(1000 * transition_time), 100)
                        t_transition = np.linspace(
                            t_segments[i-1][-1], 
                            t_segments[i][0], 
                            transition_points + 2
                        )[1:-1]  # Exclude endpoints to avoid duplicates
                    
                    # Generate transition values based on transition type
                    if transition_type == "Linear":
                        # Linear transition
                        y_transition = np.linspace(last_val, first_val, len(t_transition))
                    elif transition_type == "Smooth":
                        # Smooth (cubic) transition
                        x = np.linspace(0, 1, len(t_transition))
                        y_transition = last_val + (first_val - last_val) * (3*x**2 - 2*x**3)
                    elif transition_type == "Step":
                        # Step transition (abrupt change at midpoint)
                        y_transition = np.ones_like(t_transition) * last_val
                        mid_point = len(t_transition) // 2
                        y_transition[mid_point:] = first_val
                    elif transition_type == "Phase-Matched":
                        # Phase-matched transition - use a sinusoidal blend between the two segments
                        x = np.linspace(0, 1, len(t_transition))
                        # Use cosine blending for smooth transition
                        blend = 0.5 * (1 - np.cos(np.pi * x))
                        
                        # Get parameters for both segments
                        prev_scenario = self.sequential_scenarios[i-1]
                        curr_scenario = self.sequential_scenarios[i]
                        
                        # Calculate the waveform values for both segments at the transition points
                        if prev_scenario['waveform_type'] == "Sinusoidal" and curr_scenario['waveform_type'] == "Sinusoidal":
                            # For sinusoidal waves, we can create a smooth phase transition
                            prev_mean = prev_scenario['mean_load']
                            prev_amp = prev_scenario['load_amplitude']
                            prev_freq = prev_scenario['frequency']
                            
                            curr_mean = curr_scenario['mean_load']
                            curr_amp = curr_scenario['load_amplitude']
                            curr_freq = curr_scenario['frequency']
                            
                            # Calculate time relative to the start of each segment
                            t_rel_prev = t_transition - t_segments[i-1][0]
                            t_rel_curr = t_transition - t_segments[i][0]
                            
                            # Calculate phase at the transition point
                            prev_phase = 2 * np.pi * prev_freq * t_rel_prev[-1]
                            curr_phase = 0  # Start of the current segment
                            
                            # Generate waveforms for both segments at transition points
                            y_prev = prev_mean + prev_amp * np.sin(2 * np.pi * prev_freq * t_rel_prev + prev_phase)
                            y_curr = curr_mean + curr_amp * np.sin(2 * np.pi * curr_freq * t_rel_curr + curr_phase)
                            
                            # Blend the two waveforms
                            y_transition = (1 - blend) * y_prev + blend * y_curr
                        else:
                            # For other waveform types, use simple blending
                            y_transition = last_val + blend * (first_val - last_val)
                    
                    # Add transition to combined arrays if needed
                    if len(t_transition) > 0:
                        t_combined = np.append(t_combined, t_transition)
                        y_combined = np.append(y_combined, y_transition)
                    
                    # Add current segment (skip first point to avoid duplication)
                    t_combined = np.append(t_combined, t_segments[i][1:])
                    y_combined = np.append(y_combined, y_segments[i][1:])
            
            # Create the load_mat matrix (time in first column, load in second column)
            load_mat = np.column_stack((t_combined, y_combined))
            
            # Create the component names and units
            comp = np.array([["Load", "N"]], dtype=object)
            
            # Calculate total cycles as the sum of all scenarios' cycles
            total_cycles = sum([scenario['total_cycles'] for scenario in self.sequential_scenarios])
            
            # Create the out_strct structure as expected by FatLab
            out_strct = {
                'desc': description + " (Sequential)",
                'num_rep': num_rep,
                'num_comp': 1,  # Number of load components (just one for now)
                'num_time': len(t_combined),  # Number of time steps
                'comp': comp,
                'load_mat': load_mat,
                'total_cycles': total_cycles,  # Add total cycles
                'scenarios': self.sequential_scenarios,  # Store the individual scenarios for reference
                'transition_type': transition_type,
                'transition_cycles': transition_cycles,
                'match_phase': match_phase
            }
            
            # Save to a .mat file
            sio.savemat(output_file, {'out_strct': out_strct})
            
            # Display a single success message at the end
            messagebox.showinfo("Export Successful", f"Sequential loading data exported to {output_file}")
            self.status_var.set(f"Sequential loading data exported to {output_file}")
            
        except Exception as e:
            messagebox.showerror("Export Error", f"Failed to export sequential data: {str(e)}")
            self.status_var.set("Error exporting sequential data")
    
    def export_sequential_to_excel(self):
        """Export sequential loading scenarios to an Excel file"""
        success_message = ""  # Initialize success message
        
        try:
            # Get the output file path
            output_file = self.output_file.get()
            if not output_file.endswith('.xlsx'):
                output_file = os.path.splitext(output_file)[0] + '.xlsx'
            
            # Get export parameters
            num_rep = self.num_repetitions.get()
            transition_type = self.transition_type.get()
            transition_cycles = self.transition_cycles.get()
            match_phase = self.match_phase.get()
            separate_export = self.separate_export.get()
            
            # Create a workbook
            wb = openpyxl.Workbook()
            
            if separate_export:
                try:
                    # Remove the default sheet
                    default_sheet = wb.active
                    wb.remove(default_sheet)
                    
                    # Export each scenario to a separate sheet
                    for i, scenario in enumerate(self.sequential_scenarios, 1):
                        # Create a new sheet for each scenario
                        ws = wb.create_sheet(title=f"LC{i}")
                        
                        # Calculate points per cycle (15 points per cycle)
                        points_per_cycle = 15
                        total_cycles = scenario['total_cycles']
                        total_points = points_per_cycle * total_cycles
                        
                        # Check against Excel limit and adjust if necessary
                        max_points = 1000000  # Safe limit below Excel's maximum
                        if total_points > max_points:
                            points_per_cycle = max(2, int(max_points / total_cycles))
                            total_points = points_per_cycle * total_cycles
                        
                        # Generate time points for this scenario
                        frequency = scenario['frequency']
                        t = np.linspace(0, total_cycles/frequency, total_points)
                        
                        # Generate the waveform
                        mean = scenario['mean_load']
                        amplitude = scenario['load_amplitude']
                        phase_shift = scenario['phase_shift']
                        waveform = scenario['waveform_type']
                        
                        if waveform == "Sinusoidal":
                            y = mean + amplitude * np.sin(2 * np.pi * frequency * t + np.deg2rad(phase_shift))
                        elif waveform == "Triangular":
                            y = mean + amplitude * (2 * np.abs(2 * (frequency * t - np.floor(frequency * t + 0.5))) - 1)
                        elif waveform == "Square":
                            y = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * t + np.deg2rad(phase_shift)))
                        else:  # Custom or fallback
                            y = mean + amplitude * np.sin(2 * np.pi * frequency * t + np.deg2rad(phase_shift))
                        
                        # Apply stochastic aspects if enabled
                        if scenario['enable_stochastic']:
                            distribution = scenario['distribution_type']
                            param1 = scenario['distribution_param1']
                            param2 = scenario['distribution_param2']
                            noise_level = scenario['noise_level'] / 100.0
                            
                            if distribution == "Gaussian":
                                noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t))
                            elif distribution == "Weibull":
                                noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t))
                            elif distribution == "Log-normal":
                                noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t))
                            elif distribution == "Exponential":
                                noise = stats.expon.rvs(scale=param1, size=len(t))
                            
                            scaled_noise = noise * amplitude * noise_level
                            y = y + scaled_noise
                        
                        # Set up the sheet
                        ws['A1'] = "n"
                        ws['B1'] = num_rep
                        
                        # Set headers
                        ws['A2'] = "Time"
                        ws['B2'] = "Fx"
                        ws['G2'] = "amplitude"
                        ws['H2'] = "mean"
                        ws['I2'] = "phase"
                        ws['J2'] = "Period"
                        ws['K2'] = "frequency"
                        ws['L2'] = "cycles"
                        
                        # Set units and parameters
                        ws['A3'] = "s"
                        ws['B3'] = "kN"
                        ws['G3'] = "Fx"
                        ws['H3'] = amplitude
                        ws['I3'] = mean
                        ws['J3'] = phase_shift
                        ws['K3'] = 1.0/frequency if frequency > 0 else 0
                        ws['L3'] = frequency
                        ws['M3'] = total_cycles
                        
                        # Add time and load data
                        for row, (time_val, load_val) in enumerate(zip(t, y), 4):
                            cell = ws[f'A{row}']
                            cell.value = time_val
                            cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                            
                            cell = ws[f'B{row}']
                            cell.value = load_val
                            cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                    
                    # Add a summary sheet
                    ws_info = wb.create_sheet(title="Scenarios", index=0)  # Place summary sheet first
                    headers = ["Scenario", "Waveform", "Mean", "Amplitude", "Frequency", "Phase", "Cycles"]
                    for col, header in enumerate(headers, 1):
                        ws_info.cell(row=1, column=col, value=header)
                    
                    for row, scenario in enumerate(self.sequential_scenarios, 2):
                        ws_info.cell(row=row, column=1, value=f"Scenario {row-1}")
                        ws_info.cell(row=row, column=2, value=scenario['waveform_type'])
                        ws_info.cell(row=row, column=3, value=scenario['mean_load'])
                        ws_info.cell(row=row, column=4, value=scenario['load_amplitude'])
                        ws_info.cell(row=row, column=5, value=scenario['frequency'])
                        ws_info.cell(row=row, column=6, value=scenario['phase_shift'])
                        ws_info.cell(row=row, column=7, value=scenario['total_cycles'])
                    
                    # Save the workbook
                    wb.save(output_file)
                    
                    # Set success message for display at the end
                    success_message = f"Sequential scenarios exported to separate sheets in {output_file}"
                    
                except Exception as e:
                    raise Exception(f"Error in separate sheet export: {str(e)}")
            else:
                # Generate the sequential waveform with transitions
                t_segments = []
                y_segments = []
                total_time = 0
                
                # Process each scenario
                for i, scenario in enumerate(self.sequential_scenarios):
                    mean = scenario['mean_load']
                    amplitude = scenario['load_amplitude']
                    waveform = scenario['waveform_type']
                    frequency = scenario['frequency']
                    phase_shift_deg = scenario['phase_shift']
                    
                    # If phase matching is enabled and this is not the first scenario, calculate the phase shift
                    # to match the phase at the transition point
                    if match_phase and i > 0 and waveform in ["Sinusoidal", "Square"] and self.sequential_scenarios[i-1]['waveform_type'] in ["Sinusoidal", "Square"]:
                        # Get the last value of the previous segment
                        prev_segment = y_segments[i-1]
                        last_val = prev_segment[-1]
                        
                        # Calculate the phase shift needed to start this segment at the same value
                        normalized_val = (last_val - mean) / amplitude
                        # Clamp to [-1, 1] to handle numerical errors
                        normalized_val = max(-1.0, min(1.0, normalized_val))
                        
                        # Calculate the phase shift in radians
                        if waveform == "Sinusoidal":
                            # For sinusoidal, we use arcsin
                            phase_shift_rad = np.arcsin(normalized_val)
                        else:  # Square
                            # For square wave, we use a simpler approach
                            phase_shift_rad = 0 if normalized_val >= 0 else np.pi
                        
                        # Convert to degrees for display
                        phase_shift_deg = np.rad2deg(phase_shift_rad)
                    else:
                        # Use the original phase shift
                        phase_shift_rad = np.deg2rad(phase_shift_deg)
                    
                    # Calculate time for this segment
                    cycles = scenario['total_cycles']
                    segment_time = cycles / frequency
                    
                    # Generate time points for this segment
                    # Increase resolution to at least 1000 points per second
                    points_per_second = 1000
                    min_points = int(segment_time * points_per_second)
                    t_segment = np.linspace(total_time, total_time + segment_time, max(min_points, int(1000 * segment_time)))
                    
                    # Generate the waveform
                    if waveform == "Sinusoidal":
                        y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                    elif waveform == "Triangular":
                        y_segment = mean + amplitude * (2 * np.abs(2 * (frequency * (t_segment - total_time) - np.floor(frequency * (t_segment - total_time) + 0.5))) - 1)
                    elif waveform == "Square":
                        y_segment = mean + amplitude * np.sign(np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad))
                    else:  # Custom or fallback
                        y_segment = mean + amplitude * np.sin(2 * np.pi * frequency * (t_segment - total_time) + phase_shift_rad)
                    
                    # Apply stochastic aspects if enabled for this scenario
                    if scenario['enable_stochastic']:
                        distribution = scenario['distribution_type']
                        param1 = scenario['distribution_param1']
                        param2 = scenario['distribution_param2']
                        noise_level = scenario['noise_level'] / 100.0
                        
                        if distribution == "Gaussian":
                            noise = stats.norm.rvs(loc=param1, scale=param2, size=len(t_segment))
                        elif distribution == "Weibull":
                            noise = stats.weibull_min.rvs(c=param2, scale=param1, size=len(t_segment))
                        elif distribution == "Log-normal":
                            noise = stats.lognorm.rvs(s=param2, scale=np.exp(param1), size=len(t_segment))
                        elif distribution == "Exponential":
                            noise = stats.expon.rvs(scale=param1, size=len(t_segment))
                        
                        scaled_noise = noise * amplitude * noise_level
                        y_segment = y_segment + scaled_noise
                    
                    # Add to the segments
                    t_segments.append(t_segment)
                    y_segments.append(y_segment)
                    
                    # Update total time
                    total_time += segment_time
                
                # Apply transitions between segments
                t_combined = np.array([])
                y_combined = np.array([])
                
                for i in range(len(self.sequential_scenarios)):
                    if i == 0:
                        # First segment, no transition needed at the beginning
                        t_combined = np.append(t_combined, t_segments[i])
                        y_combined = np.append(y_combined, y_segments[i])
                    else:
                        # Apply transition between previous and current segment
                        prev_segment = y_segments[i-1]
                        curr_segment = y_segments[i]
                        
                        # Get the last value of the previous segment
                        last_val = prev_segment[-1]
                        
                        # Get the first value of the current segment
                        first_val = curr_segment[0]
                        
                        # Calculate transition time
                        transition_time = transition_cycles / self.sequential_scenarios[i]['frequency']
                        
                        # Generate transition time points - ensure no duplication with segment endpoints
                        if transition_type == "None":
                            # For "None" transition, just connect the segments without duplicating points
                            t_transition = np.array([])
                            y_transition = np.array([])
                        else:
                            # Ensure high resolution for transitions
                            transition_points = max(int(1000 * transition_time), 100)
                            t_transition = np.linspace(
                                t_segments[i-1][-1], 
                                t_segments[i][0], 
                                transition_points + 2
                            )[1:-1]  # Exclude endpoints to avoid duplicates
                        
                        # Generate transition values based on transition type
                        if transition_type == "Linear":
                            # Linear transition
                            y_transition = np.linspace(last_val, first_val, len(t_transition))
                        elif transition_type == "Smooth":
                            # Smooth (cubic) transition
                            x = np.linspace(0, 1, len(t_transition))
                            y_transition = last_val + (first_val - last_val) * (3*x**2 - 2*x**3)
                        elif transition_type == "Step":
                            # Step transition (abrupt change at midpoint)
                            y_transition = np.ones_like(t_transition) * last_val
                            mid_point = len(t_transition) // 2
                            y_transition[mid_point:] = first_val
                        elif transition_type == "Phase-Matched":
                            # Phase-matched transition - use a sinusoidal blend between the two segments
                            x = np.linspace(0, 1, len(t_transition))
                            # Use cosine blending for smooth transition
                            blend = 0.5 * (1 - np.cos(np.pi * x))
                            
                            # Get parameters for both segments
                            prev_scenario = self.sequential_scenarios[i-1]
                            curr_scenario = self.sequential_scenarios[i]
                            
                            # Calculate the waveform values for both segments at the transition points
                            if prev_scenario['waveform_type'] == "Sinusoidal" and curr_scenario['waveform_type'] == "Sinusoidal":
                                # For sinusoidal waves, we can create a smooth phase transition
                                prev_mean = prev_scenario['mean_load']
                                prev_amp = prev_scenario['load_amplitude']
                                prev_freq = prev_scenario['frequency']
                                
                                curr_mean = curr_scenario['mean_load']
                                curr_amp = curr_scenario['load_amplitude']
                                curr_freq = curr_scenario['frequency']
                                
                                # Calculate time relative to the start of each segment
                                t_rel_prev = t_transition - t_segments[i-1][0]
                                t_rel_curr = t_transition - t_segments[i][0]
                                
                                # Calculate phase at the transition point
                                prev_phase = 2 * np.pi * prev_freq * t_rel_prev[-1]
                                curr_phase = 0  # Start of the current segment
                                
                                # Generate waveforms for both segments at transition points
                                y_prev = prev_mean + prev_amp * np.sin(2 * np.pi * prev_freq * t_rel_prev + prev_phase)
                                y_curr = curr_mean + curr_amp * np.sin(2 * np.pi * curr_freq * t_rel_curr + curr_phase)
                                
                                # Blend the two waveforms
                                y_transition = (1 - blend) * y_prev + blend * y_curr
                            else:
                                # For other waveform types, use simple blending
                                y_transition = last_val + blend * (first_val - last_val)
                        
                        # Add transition to combined arrays if needed
                        if len(t_transition) > 0:
                            t_combined = np.append(t_combined, t_transition)
                            y_combined = np.append(y_combined, y_transition)
                        
                        # Add current segment (skip first point to avoid duplication)
                        t_combined = np.append(t_combined, t_segments[i][1:])
                        y_combined = np.append(y_combined, y_segments[i][1:])
                    
                    # Calculate total cycles as the sum of all scenarios' cycles
                    total_cycles = sum([scenario['total_cycles'] for scenario in self.sequential_scenarios])
                    
                    # Create a workbook and add a worksheet
                    wb = openpyxl.Workbook()
                    ws = wb.active
                    ws.title = "LC1"  # Name the sheet LC1
                    
                    # Reduce data density to fit Excel limits while maintaining waveform quality
                    max_points = 1000000  # Safe limit below Excel's maximum
                    if len(t_combined) > max_points:
                        # Calculate step size to reduce points
                        step = len(t_combined) // max_points
                        t_combined = t_combined[::step]
                        y_combined = y_combined[::step]
                    
                    # Set the number of repetitions in cell B1
                    ws['A1'] = "n"
                    ws['B1'] = num_rep
                    
                    # Set the column headers in row 2
                    ws['A2'] = "Time"
                    ws['B2'] = "Fx"
                    ws['G2'] = "amplitude"
                    ws['H2'] = "mean"
                    ws['I2'] = "phase"
                    ws['J2'] = "Period"
                    ws['K2'] = "frequency"
                    ws['L2'] = "cycles"  # Add cycles column
                    
                    # Calculate effective amplitude and mean for the sequential waveform
                    effective_mean = np.mean(y_combined)
                    effective_amplitude = (np.max(y_combined) - np.min(y_combined)) / 2
                    
                    # Use the lowest frequency as the effective frequency
                    effective_frequency = min([scenario['frequency'] for scenario in self.sequential_scenarios])
                    effective_period = 1.0 / effective_frequency if effective_frequency > 0 else 0
                    
                    # Set the units in row 3
                    ws['A3'] = "s"
                    ws['B3'] = "kN"
                    ws['G3'] = "Fx"
                    ws['H3'] = effective_amplitude
                    ws['I3'] = effective_mean
                    ws['J3'] = 0  # Phase shift not applicable for sequential
                    ws['K3'] = effective_period
                    ws['L3'] = effective_frequency
                    ws['M3'] = total_cycles  # Add total cycles
                    
                    # Add time and load data starting from row 4
                    for i, (time_val, load_val) in enumerate(zip(t_combined, y_combined), 4):  # Start from row 4
                        cell = ws[f'A{i}']
                        cell.value = time_val
                        cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                        
                        cell = ws[f'B{i}']
                        cell.value = load_val
                        cell.number_format = '0.000000'  # Set number format to show 6 decimal places
                    
                    # Add a second sheet with information about the individual scenarios
                    ws_info = wb.create_sheet(title="Scenarios")
                    
                    # Add headers
                    headers = ["Scenario", "Waveform", "Mean", "Amplitude", "Frequency", "Phase", "Cycles"]
                    for col, header in enumerate(headers, 1):
                        ws_info.cell(row=1, column=col, value=header)
                    
                    # Add scenario information
                    for row, scenario in enumerate(self.sequential_scenarios, 2):
                        ws_info.cell(row=row, column=1, value=f"Scenario {row-1}")
                        ws_info.cell(row=row, column=2, value=scenario['waveform_type'])
                        ws_info.cell(row=row, column=3, value=scenario['mean_load'])
                        ws_info.cell(row=row, column=4, value=scenario['load_amplitude'])
                        ws_info.cell(row=row, column=5, value=scenario['frequency'])
                        ws_info.cell(row=row, column=6, value=scenario['phase_shift'])
                        ws_info.cell(row=row, column=7, value=scenario['total_cycles'])
                    
                    # Add transition information
                    ws_info.cell(row=len(self.sequential_scenarios) + 3, column=1, value="Transition Type:")
                    ws_info.cell(row=len(self.sequential_scenarios) + 3, column=2, value=transition_type)
                    ws_info.cell(row=len(self.sequential_scenarios) + 4, column=1, value="Transition Cycles:")
                    ws_info.cell(row=len(self.sequential_scenarios) + 4, column=2, value=transition_cycles)
                    ws_info.cell(row=len(self.sequential_scenarios) + 5, column=1, value="Phase Matching:")
                    ws_info.cell(row=len(self.sequential_scenarios) + 5, column=2, value="Enabled" if match_phase else "Disabled")
                    
                    # Save the workbook
                    wb.save(output_file)
                    
                    # Set success message for display at the end
                    success_message = f"Sequential loading data exported to {output_file}"
                
            # Display a single success message at the end
            if success_message:
                messagebox.showinfo("Export Successful", success_message)
                self.status_var.set(success_message)
            
        except Exception as e:
            messagebox.showerror("Export Error", f"Failed to export sequential data: {str(e)}")
            self.status_var.set("Error exporting sequential data")

    def update_ui_from_config(self):
        """Update the UI based on the loaded configuration"""
        # Update the preview based on the mode
        if self.is_sequential_mode.get() and self.sequential_scenarios:
            # Switch to the sequential tab
            self.notebook.select(self.sequential_tab)
            # Preview the sequential loading
            self.preview_sequential()
        elif self.is_superposition_mode.get() and self.loading_scenarios:
            # Switch to the superposition tab
            self.notebook.select(self.superposition_tab)
            # Preview the superposition
            self.preview_superposition()
        else:
            # Default to the basic tab
            self.notebook.select(self.basic_tab)
            # Update the preview
            self.update_preview()
        
        # Update the stochastic UI based on the enable_stochastic setting
        self.update_stochastic_ui()
        
        # Update the export tab
        self.notebook.select(self.export_tab)
        
        # Return to the appropriate tab based on the mode
        if self.is_sequential_mode.get():
            self.notebook.select(self.sequential_tab)
        elif self.is_superposition_mode.get():
            self.notebook.select(self.superposition_tab)
        else:
            self.notebook.select(self.basic_tab)

    def on_transition_type_changed(self, event):
        """Handle changes to the transition type"""
        transition_type = self.transition_type.get()
        
        # If Phase-Matched is selected, force enable phase matching and disable the checkbox
        if transition_type == "Phase-Matched":
            self.match_phase.set(True)
            self.match_phase_checkbox.configure(state="disabled")
        else:
            # For other transition types, enable the checkbox
            self.match_phase_checkbox.configure(state="normal")
        
        # If there are scenarios, update the preview
        if self.sequential_scenarios:
            self.preview_sequential()

    def generate_waveform(self):
        """Generate the waveform based on the current parameters"""
        try:
            # Get the total number of points based on frequency and total cycles
            total_time = self.total_cycles.get() / self.frequency.get()
            num_points = int(total_time * self.sampling_rate)
            time = np.linspace(0, total_time, num_points)
            
            if self.use_reference_mode.get():
                # Calculate mean and amplitude from reference and target loads using the updated formula
                target_mean = (self.target_max_load.get()/self.ref_pos_load.get() + self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                target_amplitude = (self.target_max_load.get()/self.ref_pos_load.get() - self.target_min_load.get()/abs(self.ref_neg_load.get())) / 2
                
                # Generate base waveform
                if self.waveform_type.get() == "Sinusoidal":
                    base_waveform = np.sin(2 * np.pi * self.frequency.get() * time)
                elif self.waveform_type.get() == "Triangular":
                    base_waveform = signal.sawtooth(2 * np.pi * self.frequency.get() * time, width=0.5)
                elif self.waveform_type.get() == "Square":
                    base_waveform = signal.square(2 * np.pi * self.frequency.get() * time)
                else:
                    raise ValueError("Unsupported waveform type")
                
                # Apply phase shift
                phase_shift_rad = np.radians(self.phase_shift.get())
                base_waveform = np.roll(base_waveform, int(phase_shift_rad * num_points / (2 * np.pi)))
                
                # Scale and offset the waveform
                waveform = base_waveform * target_amplitude + target_mean
                
            else:
                # Standard mode - use mean load and amplitude directly
                if self.waveform_type.get() == "Sinusoidal":
                    waveform = self.mean_load.get() + self.load_amplitude.get() * np.sin(2 * np.pi * self.frequency.get() * time)
                elif self.waveform_type.get() == "Triangular":
                    waveform = self.mean_load.get() + self.load_amplitude.get() * signal.sawtooth(2 * np.pi * self.frequency.get() * time, width=0.5)
                elif self.waveform_type.get() == "Square":
                    waveform = self.mean_load.get() + self.load_amplitude.get() * signal.square(2 * np.pi * self.frequency.get() * time)
                else:
                    raise ValueError("Unsupported waveform type")
                
                # Apply phase shift
                phase_shift_rad = np.radians(self.phase_shift.get())
                waveform = np.roll(waveform, int(phase_shift_rad * num_points / (2 * np.pi)))
            
            return time, waveform
            
        except Exception as e:
            messagebox.showerror("Error", f"Failed to generate waveform: {str(e)}")
            return None, None

def main():
    """Main function to run the application"""
    # Create the root window
    root = tk.Tk()
    
    # Configure Windows 95 style
    Win95Style.configure_styles()
    
    # Create the application
    app = FatigueSetupGenerator(root, demo_mode=args.demo)
    
    # Start the main loop
    root.mainloop()

if __name__ == "__main__":
    main() 