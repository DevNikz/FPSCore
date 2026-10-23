using System;
using System.Collections.Generic;
using System.Linq;
using UnityEditor;
using UnityEditor.PackageManager;
using UnityEditor.PackageManager.Requests;
using UnityEngine;

/// <summary>
/// Dependencies Installer For FPS Core
/// - Auto Installs/Updates the ff dependencies:
///     - Cinemachine 3.1.7
///     - InputSystem 1.19.0
/// </summary>
[InitializeOnLoad]
static class FpsDependencyInstaller
{
    // name, minimum version, version to install
    private static readonly (string name, string min, string install)[] Dependencies =
    {
        ("com.unity.cinemachine", "3.1.7", "3.1.7"),
        ("com.unity.inputsystem", "1.19.0", "1.19.0"),
    };

    private static ListRequest _list;
    private static AddRequest _add;
    private static Queue<string> _toInstall;

    static FpsDependencyInstaller()
    {
        // No session guard: the check is cheap and skips anything already satisfied,
        // so it's safe to run after every domain reload.
        EditorApplication.delayCall += Run;
    }

    [MenuItem("Tools/FPS Core/Install Dependencies")]
    private static void Run()
    {
        Debug.Log("FPSCore: checking dependencies...");
        _list = Client.List(offlineMode: false, includeIndirectDependencies: true);
        EditorApplication.update += OnList;
    }

    private static void OnList()
    {
        if (!_list.IsCompleted)
            return;
        EditorApplication.update -= OnList;

        if (_list.Status != StatusCode.Success)
        {
            Debug.LogWarning($"FPSCore: couldn't read package list ({_list.Error?.message}).");
            return;
        }

        _toInstall = new Queue<string>();
        foreach (var dep in Dependencies)
        {
            var installed = _list.Result.FirstOrDefault(p => p.name == dep.name);
            if (installed != null && IsAtLeast(installed.version, dep.min))
                continue;
            _toInstall.Enqueue($"{dep.name}@{dep.install}");
        }

        InstallNext();
    }

    // Packages are added one at a time; the Package Manager rejects overlapping requests.
    private static void InstallNext()
    {
        if (_toInstall.Count == 0)
            return;
        string id = _toInstall.Dequeue();
        Debug.Log($"FPSCore: installing {id}");
        _add = Client.Add(id);
        EditorApplication.update += OnAdd;
    }

    private static void OnAdd()
    {
        if (!_add.IsCompleted)
            return;
        EditorApplication.update -= OnAdd;

        if (_add.Status == StatusCode.Success)
            Debug.Log($"FPSCore: installed {_add.Result.name}@{_add.Result.version}");
        else
            Debug.LogError($"FPSCore: install failed: {_add.Error.message}");

        InstallNext();
    }

    private static bool IsAtLeast(string have, string min)
    {
        try
        {
            return new Version(have.Split('-')[0]) >= new Version(min);
        }
        catch
        {
            return false;
        }
    }
}
