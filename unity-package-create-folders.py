import argparse
import datetime
import shutil
from pathlib import Path

# [Unity - Manual: Package layout](https://docs.unity3d.com/Manual/cus-layout.html)


parser = argparse.ArgumentParser(prog='unity-package-create-folders',
                                 description='create layout for an unity package')

parser.add_argument('-c', '--company-name', dest='company_name', default='company')
parser.add_argument('-p', '--package-name', dest='package_name', default='package')
parser.add_argument('--clean', dest='clean', action='store_true')


def clean(structure, path):
    for key in structure:
        if type(structure[key]) is dict:
            folder_path = path / key
            shutil.rmtree(folder_path, ignore_errors=True)
        else:
            file_path = path / key
            file_path.unlink(missing_ok=True)


def create_structure(structure, path):
    for key in structure:
        if type(structure[key]) is dict:
            folder_path = path / key
            folder_path.mkdir(exist_ok=True, parents=False)
            create_structure(structure[key], folder_path)
        else:
            file_path = path / key
            with file_path.open('w', encoding='utf-8') as file:
                file.write(structure[key])


def define_structure(company_name, package_name):
    package_manifest_file = f'''{{
  "name": "com.{company_name}.{package_name}",
  "version": "0.0.0",
  "__displayName": "Package template",
  "__description": "This is an package template.",
  "__unity": "2019.1",
  "__unityRelease": "0b5",
  "__documentationUrl": "https://example.com/",
  "__changelogUrl": "https://example.com/changelog.html",
  "__licensesUrl": "https://example.com/licensing.html",
  "__dependencies": {{
    "com.company-name.some-package": "1.0.0",
    "com.company-name.other-package": "2.0.0"
 }},
 "__keywords": [
    "keyword1",
    "keyword2",
    "keyword3"
  ],
  "__author": {{
    "name": "Unity",
    "email": "unity@example.com",
    "url": "https://www.unity3d.com"
  }}
}}
      '''

    license_file = f'''Copyright (c) {datetime.date.today().year} Alexander Yu Shamin and others

Permission is hereby granted, free of charge, to any person obtaining
a copy of this software and associated documentation files (the
"Software"), to deal in the Software without restriction, including
without limitation the rights to use, copy, modify, merge, publish,
distribute, sublicense, and/or sell copies of the Software, and to
permit persons to whom the Software is furnished to do so, subject to
the following conditions:

The above copyright notice and this permission notice shall be
included in all copies or substantial portions of the Software.

THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND,
EXPRESS OR IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF
MERCHANTABILITY, FITNESS FOR A PARTICULAR PURPOSE AND
NONINFRINGEMENT. IN NO EVENT SHALL THE AUTHORS OR COPYRIGHT HOLDERS BE
LIABLE FOR ANY CLAIM, DAMAGES OR OTHER LIABILITY, WHETHER IN AN ACTION
OF CONTRACT, TORT OR OTHERWISE, ARISING FROM, OUT OF OR IN CONNECTION
WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE SOFTWARE.
    '''

    editor_asmdef_file = f'''{{
"name": "{company_name}.{package_name}.Editor",
  "references": [],
  "optionalUnityReferences": [],
  "includePlatforms": [
    "Editor"
  ],
  "excludePlatforms": [],
  "allowUnsafeCode": false,
  "overrideReferences": false,
  "precompiledReferences": [],
  "autoReferenced": true,
  "defineConstraints": [],
  "versionDefines": [],
  "noEngineReferences": false
}}
    '''

    runtime_asmdef_file = f'''{{
"name": "{company_name}.{package_name}",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": []
}}
    '''

    samples_asmdef_file = f'''{{
"name": "{company_name}.{package_name}.Samples",
    "references": [],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": false,
    "precompiledReferences": [],
    "autoReferenced": true,
    "defineConstraints": [],
    "versionDefines": [],
    "noEngineReferences": false
}}
    '''

    editor_tests_asmdef_file = f'''{{
"name": "{company_name}.{package_name}.Editor.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [
        "Editor"
    ],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}}
    '''

    runtime_tests_asmdef_file = f'''{{
"name": "{company_name}.{package_name}.Tests",
    "references": [
        "UnityEngine.TestRunner",
        "UnityEditor.TestRunner"
    ],
    "includePlatforms": [],
    "excludePlatforms": [],
    "allowUnsafeCode": false,
    "overrideReferences": true,
    "precompiledReferences": [
        "nunit.framework.dll"
    ],
    "autoReferenced": false,
    "defineConstraints": [
        "UNITY_INCLUDE_TESTS"
    ],
    "versionDefines": [],
    "noEngineReferences": false
}}
    '''

    editor_assembly_info_file = f'''using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("{company_name}.{package_name}.Editor")]
    '''

    runtime_assembly_info_file = f'''using System.Runtime.CompilerServices;

[assembly: InternalsVisibleTo("{company_name}.{package_name}")]
    '''

    readme_file = f'''# com.{company_name}.{package_name}
    '''

    readme_template_file = f'''# Unity Package Template
    
For initializing you can:
- change the package template presented in the repository
- create a package from template:
    - clean: `unity-create-template-package.py --clean`
    - create: `unity-create-template-package.py -c <company name> -p <package name>`
    '''

    contributing_file = '''# Contributing

Thank you for spending time and effort contributing to this repository.
    '''

    package_documentation_file = f'''# com.{company_name}.{package_name}
    '''

    third_party_notices_file = f'''This package contains third-party software components governed by the license(s) indicated below:

Component Name: <Component Name>

License Type: "MIT"

[text](url)

    '''

    project_manifest_file = f'''{{
  "dependencies": {{
    "com.{company_name}.{package_name}": "file:../../com.{company_name}.{package_name}",
    "com.unity.ide.rider": "3.0.24",
    "com.unity.ide.visualstudio": "2.0.18",
    "com.unity.ide.vscode": "1.2.5",
    "com.unity.test-framework": "1.1.33",
    "com.unity.modules.ai": "1.0.0",
    "com.unity.modules.androidjni": "1.0.0",
    "com.unity.modules.animation": "1.0.0",
    "com.unity.modules.assetbundle": "1.0.0",
    "com.unity.modules.audio": "1.0.0",
    "com.unity.modules.cloth": "1.0.0",
    "com.unity.modules.director": "1.0.0",
    "com.unity.modules.imageconversion": "1.0.0",
    "com.unity.modules.imgui": "1.0.0",
    "com.unity.modules.jsonserialize": "1.0.0",
    "com.unity.modules.particlesystem": "1.0.0",
    "com.unity.modules.physics": "1.0.0",
    "com.unity.modules.physics2d": "1.0.0",
    "com.unity.modules.screencapture": "1.0.0",
    "com.unity.modules.terrain": "1.0.0",
    "com.unity.modules.terrainphysics": "1.0.0",
    "com.unity.modules.tilemap": "1.0.0",
    "com.unity.modules.ui": "1.0.0",
    "com.unity.modules.uielements": "1.0.0",
    "com.unity.modules.umbra": "1.0.0",
    "com.unity.modules.unityanalytics": "1.0.0",
    "com.unity.modules.unitywebrequest": "1.0.0",
    "com.unity.modules.unitywebrequestassetbundle": "1.0.0",
    "com.unity.modules.unitywebrequestaudio": "1.0.0",
    "com.unity.modules.unitywebrequesttexture": "1.0.0",
    "com.unity.modules.unitywebrequestwww": "1.0.0",
    "com.unity.modules.vehicles": "1.0.0",
    "com.unity.modules.video": "1.0.0",
    "com.unity.modules.vr": "1.0.0",
    "com.unity.modules.wind": "1.0.0"
  }},
  "testables": [
    "com.{company_name}.{package_name}"
  ]
}}
    '''

    gitignore_file = f'''# This .gitignore file should be placed at the root of your Unity project directory
#
# Get latest from https://github.com/github/gitignore/blob/master/Unity.gitignore
#
/[Ll]ibrary/
/[Tt]emp/
/[Oo]bj/
/[Bb]uild/
/[Bb]uilds/
/[Ll]ogs/
/[Mm]emoryCaptures/

# Asset meta data should only be ignored when the corresponding asset is also ignored
!/[Aa]ssets/**/*.meta

# Uncomment this line if you wish to ignore the asset store tools plugin
# /[Aa]ssets/AssetStoreTools*

# Autogenerated Jetbrains Rider plugin
[Aa]ssets/Plugins/Editor/JetBrains*

# Visual Studio cache directory
.vs/

# Gradle cache directory
.gradle/

# Autogenerated VS/MD/Consulo solution and project files
ExportedObj/
.consulo/
*.csproj
*.unityproj
*.sln
*.suo
*.tmp
*.user
*.userprefs
*.pidb
*.booproj
*.svd
*.pdb
*.mdb
*.opendb
*.VC.db

# Unity3D generated meta files
*.pidb.meta
*.pdb.meta
*.mdb.meta

# Unity3D generated file on crash reports
sysinfo.txt

# Builds
*.apk
*.unitypackage

# Crashlytics generated file
crashlytics-build.properties
    '''

    gitattributes_file = f'''###############################################################################
# https://github.com/alexkaratarakis/gitattributes
###############################################################################
* text=auto

*.gitattributes text
.gitignore      text
*.md		text

###############################################################################
# Unity
###############################################################################
*.cginc              text
*.cs                 text diff=csharp
*.shader             text

# Unity YAML
*.mat                merge=unityyamlmerge eol=lf linguist-generated
*.unity              merge=unityyamlmerge eol=lf linguist-generated
*.prefab             merge=unityyamlmerge eol=lf linguist-generated
*.asset              merge=unityyamlmerge eol=lf linguist-generated
*.meta               merge=unityyamlmerge eol=lf linguist-generated

*.controller         merge=unityyamlmerge eol=lf
*.anim 		     merge=unityyamlmerge eol=lf 
*.physicMaterial2D   merge=unityyamlmerge eol=lf
*.physicMaterial     merge=unityyamlmerge eol=lf
*.physicsMaterial2D  merge=unityyamlmerge eol=lf
*.physicsMaterial    merge=unityyamlmerge eol=lf

# Unity LFS
*.cubemap            binary
*.unitypackage       binary
# 3D models
*.3dm                binary
*.3ds                binary
*.blend              binary
*.c4d                binary
*.collada            binary
*.dae                binary
*.dxf                binary
*.FBX                binary
*.fbx                binary
*.jas                binary
*.lws                binary
*.lxo                binary
*.ma                 binary
*.max                binary
*.mb                 binary
*.obj                binary
*.ply                binary
*.skp                binary
*.stl                binary
*.ztl                binary
# Audio
*.aif                binary
*.aiff               binary
*.it                 binary
*.mod                binary
*.mp3                binary
*.ogg                binary
*.s3m                binary
*.wav                binary
*.xm                 binary
# Video
*.asf                binary
*.avi                binary
*.flv                binary
*.mov                binary
*.mp4                binary
*.mpeg               binary
*.mpg                binary
*.ogv                binary
*.wmv                binary
# Images
*.bmp                binary
*.exr                binary
*.gif                binary
*.hdr                binary
*.iff                binary
*.jpeg               binary
*.jpg                binary
*.pict               binary
*.png                binary
*.psd                binary
*.tga                binary
*.tif                binary
*.tiff               binary
# Compressed Archive
*.7z                binary
*.bz2               binary
*.gz                binary
*.rar               binary
*.tar               binary
*.zip               binary

# Compiled Dynamic Library
*.dll 		    binary
*.pdb               binary
*.so                binary
# Fonts
*.otf	            binary 
*.ttf               binary
# Executable/Installer
*.apk               binary
*.exe               binary
# Documents
*.pdf               binary
# ETC
*.a                 binary
*.rns               binary
*.reason            binary
    '''

    structure = \
        {
            f'com.{company_name}.{package_name}': {
                'Documentation~': {f'com.{company_name}.{package_name}.md': package_documentation_file},
                'Editor': {f'{company_name}.{package_name}.Editor.asmdef': editor_asmdef_file,
                           'AssemblyInfo.cs': editor_assembly_info_file},
                'Runtime': {f'{company_name}.{package_name}.asmdef': runtime_asmdef_file,
                            'AssemblyInfo.cs': runtime_assembly_info_file},
                'Samples': {f'{company_name}.{package_name}.Samples.asmdef': samples_asmdef_file},
                'Tests':
                    {
                        'Editor': {f'{company_name}.{package_name}.Editor.Tests.asmdef': editor_tests_asmdef_file},
                        'Runtime': {f'{company_name}.{package_name}.Tests.asmdef': runtime_tests_asmdef_file}
                    },
                'CHANGELOG.md': '# Changelog \n https://keepachangelog.com/en/1.0.0/',
                'LICENSE.md': license_file,
                'package.json': package_manifest_file,
                'README.md': f'# {package_name}',
                'Third Party Notices.md': third_party_notices_file
            },
            f'com.{company_name}.{package_name}.project': {
                'Assets': {},
                'Packages': {'manifest.json': project_manifest_file},
                '.gitignore': gitignore_file,
                '.gitattributes': gitattributes_file
            },
            'README.md': (readme_file, readme_template_file)[company_name == 'company' and package_name == 'package'],
            'LICENSE': license_file,
            'CONTRIBUTING.md': contributing_file
        }

    return structure


if __name__ == "__main__":
    args = parser.parse_args()
    company_name = args.company_name
    package_name = args.package_name

    structure = define_structure(company_name, package_name)

    if args.clean is True:
        clean(structure, Path('./'))
        exit(0)

    create_structure(structure, Path('./'))
