using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json.Linq;
using UnityEditor;
using UnityEngine;
using UnityEngine.VFX;

namespace MCPForUnity.Editor.Tools.Vfx
{
    /// <summary>
    /// Asset management operations for VFX Graph.
    /// Handles creating, assigning, and listing VFX assets.
    /// </summary>
    public static class VfxGraphAssets
    {
        private static readonly string[] SupportedVfxGraphVersions = { "12.1", "13.1", "14.0", "15.0", "16.0", "17.0" }; // Expanded supported versions

        private static string ValidateVfxGraphVersion()
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.visualeffectgraph");
            if (packageInfo == null || string.IsNullOrWhiteSpace(packageInfo.version))
            {
                return "Unable to determine com.unity.visualeffectgraph version.";
            }

            bool supported = SupportedVfxGraphVersions.Any(v => packageInfo.version.StartsWith(v, StringComparison.Ordinal));
            if (supported) return null;
            return $"Detected VFX Graph version '{packageInfo.version}', which is outside validated versions [{string.Join(", ", SupportedVfxGraphVersions)}].";
        }

        /// <summary>
        /// Creates a new VFX Graph asset file from a template.
        /// </summary>
        public static object CreateAsset(JObject @params)
        {
            string assetName = @params["assetName"]?.ToString()?.Trim();
            string folderPath = @params["folderPath"]?.ToString() ?? "Assets/VFX";
            string template = @params["template"]?.ToString() ?? "empty";

            if (string.IsNullOrEmpty(assetName))
            {
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "assetName is required");
            }

            if (assetName.IndexOfAny(System.IO.Path.GetInvalidFileNameChars()) >= 0)
            {
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "assetName contains invalid file name characters");
            }

            if (!folderPath.StartsWith("Assets/", StringComparison.Ordinal))
            {
                folderPath = "Assets/" + folderPath.TrimStart('/');
            }

            if (folderPath.Contains("../", StringComparison.Ordinal) || folderPath.Contains("/..", StringComparison.Ordinal))
            {
                return VfxToolContract.Error(VfxErrorCodes.ValidationError, "Path traversal is not allowed for folderPath");
            }

            string versionWarning = ValidateVfxGraphVersion();
            var pipelineSupport = VfxPipelineSupport.ValidateUrpSupport();

            // Ensure folder exists
            if (!AssetDatabase.IsValidFolder(folderPath))
            {
                string[] folders = folderPath.Split('/');
                string currentPath = folders[0];
                for (int i = 1; i < folders.Length; i++)
                {
                    string newPath = currentPath + "/" + folders[i];
                    if (!AssetDatabase.IsValidFolder(newPath))
                    {
                        AssetDatabase.CreateFolder(currentPath, folders[i]);
                    }
                    currentPath = newPath;
                }
            }

            string assetPath = $"{folderPath}/{assetName}.vfx";

            // Check if asset already exists
            if (AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath) != null)
            {
                bool overwrite = @params["overwrite"]?.ToObject<bool>() ?? false;
                if (!overwrite)
                {
                    return VfxToolContract.Error(
                        VfxErrorCodes.ValidationError,
                        $"Asset already exists at {assetPath}. Set overwrite=true to replace.");
                }
                AssetDatabase.DeleteAsset(assetPath);
            }

            // Find template asset and copy it
            string templatePath = FindTemplate(template);
            
            // Fallback for empty graph if template not found
            if (string.IsNullOrEmpty(templatePath) && template == "empty")
            {
                 // Create a minimal VFX graph manually if possible? 
                 // Or better, just rely on the user having templates.
                 // But typically Unity provides "Packages/com.unity.visualeffectgraph/Editor/Templates/Empty.vfx"
            }

            VisualEffectAsset newAsset = null;

            if (!string.IsNullOrEmpty(templatePath))
            {
                 // Copy the asset to create a new VFX Graph asset
                 if (!AssetDatabase.CopyAsset(templatePath, assetPath))
                 {
                     return VfxToolContract.Error(VfxErrorCodes.InternalException, $"Failed to copy VFX template from {templatePath}");
                 }
                 AssetDatabase.Refresh();
                 newAsset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath);
            }
            else
            {
                return VfxToolContract.Error(
                    VfxErrorCodes.NotFound,
                    "VFX template not found. Add a .vfx template asset or install VFX Graph templates.",
                    new { searched_template = template });
            }

            if (newAsset == null)
            {
                return VfxToolContract.Error(VfxErrorCodes.InternalException, "Failed to create VFX asset. Try using a template from list_templates.");
            }

            return VfxToolContract.Success(
                $"Created VFX asset: {assetPath}",
                new
                {
                    assetPath = assetPath,
                    assetName = newAsset.name,
                    template = template
                },
                new
                {
                    warning = versionWarning,
                    active_pipeline = pipelineSupport.activePipeline,
                    urp_supported = pipelineSupport.supported
                });
        }

        /// <summary>
        /// Finds VFX template path by name.
        /// </summary>
        private static string FindTemplate(string templateName)
        {
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.visualeffectgraph");

            var searchPaths = new List<string>();

            if (packageInfo != null)
            {
                searchPaths.Add(System.IO.Path.Combine(packageInfo.resolvedPath, "Editor/Templates"));
                searchPaths.Add(System.IO.Path.Combine(packageInfo.resolvedPath, "Samples"));
            }

            searchPaths.Add("Assets/VFX/Templates");
            
            // Also add standard package paths just in case resolvedPath is tricky
             searchPaths.Add("Packages/com.unity.visualeffectgraph/Editor/Templates");

            string[] templatePatterns = new[]
            {
                $"{templateName}.vfx",
                $"VFX{templateName}.vfx",
                $"Simple{templateName}.vfx",
                $"{templateName}VFX.vfx",
                $"{templateName} Graph.vfx" // "Empty Graph.vfx"
            };

            foreach (string basePath in searchPaths)
            {
                string searchRoot = basePath;
                // If it's a relative path starting with Assets/ or Packages/, we might need to resolve it 
                // But Directory.GetFiles expects absolute system paths usually, or relative to project root?
                // Application.dataPath is <project>/Assets
                
                if (basePath.StartsWith("Assets/"))
                {
                    searchRoot = System.IO.Path.Combine(System.IO.Path.GetDirectoryName(Application.dataPath), basePath); 
                }
                else if (basePath.StartsWith("Packages/"))
                {
                     // Resolve packages path logic if needed, but let's assume resolvedPath used above is absolute.
                     // If we manually added "Packages/...", we skip validation for now or use AssetDatabase.
                }

                if (!System.IO.Directory.Exists(searchRoot) && !basePath.StartsWith("Packages/"))
                {
                    continue;
                }
                
                // If checking packages via AssetDatabase
                if (basePath.StartsWith("Packages/"))
                {
                     // Use AssetDatabase to find templates in packages
                     // "Empty Graph" is usually at Packages/com.unity.visualeffectgraph/Editor/Templates/Empty Graph.vfx
                     string p = $"{basePath}/{templateName}.vfx";
                     if (System.IO.File.Exists(p)) return p;
                     
                     // Try AssetDatabase.FindAssets inside the loop?
                }

                try
                {
                    if (System.IO.Directory.Exists(searchRoot))
                    {
                        foreach (string pattern in templatePatterns)
                        {
                            string[] files = System.IO.Directory.GetFiles(searchRoot, pattern, System.IO.SearchOption.AllDirectories);
                            if (files.Length > 0)
                            {
                                // Convert to relative project path for CopyAsset?
                                // AssetDatabase.CopyAsset requires project-relative path (Assets/... or Packages/...)
                                return ToProjectRelativePath(files[0]);
                            }
                        }
                    }
                }
                catch {}
            }
            
            // Final fallback: AssetDatabase search
             string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset " + templateName);
             if (guids.Length > 0)
             {
                 return AssetDatabase.GUIDToAssetPath(guids[0]);
             }

            return null;
        }

        private static string ToProjectRelativePath(string absolutePath)
        {
            string projectPath = System.IO.Path.GetDirectoryName(Application.dataPath);
            
            // Handle Library/PackageCache paths
            // Format: .../Library/PackageCache/com.package.name@version/Path...
            // Target: Packages/com.package.name/Path...
            if (absolutePath.Contains("Library/PackageCache/"))
            {
                int index = absolutePath.IndexOf("Library/PackageCache/");
                string relative = absolutePath.Substring(index + "Library/PackageCache/".Length);
                
                string[] parts = relative.Split('/');
                if (parts.Length > 0)
                {
                    string packageDir = parts[0];
                    string packageName = packageDir;
                    if (packageName.Contains("@"))
                    {
                        packageName = packageName.Substring(0, packageName.IndexOf("@"));
                    }
                    
                    // Reconstruct path
                    string rest = string.Join("/", parts.Skip(1));
                    return $"Packages/{packageName}/{rest}";
                }
            }

            if (absolutePath.StartsWith(projectPath))
            {
                return absolutePath.Substring(projectPath.Length + 1).Replace("\\", "/");
            }
            return absolutePath;
        }

        /// <summary>
        /// Assigns a VFX asset to a VisualEffect component.
        /// </summary>
        public static object AssignAsset(JObject @params)
        {
            VisualEffect vfx = VfxGraphCommon.FindVisualEffect(@params);
            if (vfx == null)
            {
                return VfxToolContract.Error(VfxErrorCodes.NotFound, "VisualEffect component not found");
            }

            string assetPath = @params["assetPath"]?.ToString();
            if (!VfxInputValidation.TryValidateAssetPath(assetPath, ".vfx", out assetPath, out object pathError))
            {
                return pathError;
            }

            var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(assetPath);
            if (asset == null)
            {
                return VfxToolContract.Error(VfxErrorCodes.AssetNotFound, $"VFX asset not found: {assetPath}");
            }

            Undo.RecordObject(vfx, "Assign VFX Asset");
            vfx.visualEffectAsset = asset;
            EditorUtility.SetDirty(vfx);

            return VfxToolContract.Success(
                $"Assigned VFX asset '{asset.name}' to {vfx.gameObject.name}",
                new
                {
                    gameObject = vfx.gameObject.name,
                    assetName = asset.name,
                    assetPath = assetPath
                });
        }

        public static object ListTemplates(JObject @params)
        {
            var templates = new List<object>();

            // Search in VFX package templates
            var packageInfo = UnityEditor.PackageManager.PackageInfo.FindForAssetPath("Packages/com.unity.visualeffectgraph");
            if (packageInfo != null)
            {
                string templatesDir = System.IO.Path.Combine(packageInfo.resolvedPath, "Editor", "Templates");
                if (System.IO.Directory.Exists(templatesDir))
                {
                    foreach (var file in System.IO.Directory.GetFiles(templatesDir, "*.vfx", System.IO.SearchOption.AllDirectories))
                    {
                        string relativePath = ToProjectRelativePath(file);
                        templates.Add(new { name = System.IO.Path.GetFileNameWithoutExtension(file), path = relativePath, source = "package" });
                    }
                }
            }

            // Search in project Assets
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { "Assets" });
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                if (path.ToLowerInvariant().Contains("template"))
                {
                    templates.Add(new { name = System.IO.Path.GetFileNameWithoutExtension(path), path = path, source = "project" });
                }
            }

            return VfxToolContract.Success(
                $"Found {templates.Count} templates",
                new { count = templates.Count, templates = templates });
        }

        public static object ListAssets(JObject @params)
        {
            string scope = @params?["scope"]?.ToString() ?? "Assets";
            string[] guids = AssetDatabase.FindAssets("t:VisualEffectAsset", new[] { scope });

            var assets = new List<object>();
            foreach (var guid in guids)
            {
                string path = AssetDatabase.GUIDToAssetPath(guid);
                var asset = AssetDatabase.LoadAssetAtPath<VisualEffectAsset>(path);
                assets.Add(new
                {
                    name = asset != null ? asset.name : System.IO.Path.GetFileNameWithoutExtension(path),
                    path = path,
                    guid = guid
                });
            }

            return VfxToolContract.Success(
                $"Found {assets.Count} VFX assets",
                new { count = assets.Count, assets = assets });
        }
    }
}
