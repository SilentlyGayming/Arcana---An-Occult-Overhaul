import argparse
import os
import shutil
import struct
import subprocess
from pathlib import Path
from tempfile import TemporaryDirectory
from xml.sax.saxutils import escape
import cs_source
import localization

ROOT = Path(__file__).resolve().parents[1]


def host_copy(source, target):
    data = bytearray(source.read_bytes())
    pe = struct.unpack_from('<I', data, 0x3c)[0]
    sections = struct.unpack_from('<H', data, pe + 6)[0]
    optional_size = struct.unpack_from('<H', data, pe + 20)[0]
    optional = pe + 24
    cli = struct.unpack_from('<I', data, optional + 96 + 14 * 8)[0]
    for i in range(sections):
        row = optional + optional_size + i * 40
        virtual_size, start, raw_size, raw = struct.unpack_from('<IIII', data, row + 8)
        if start <= cli < start + max(virtual_size, raw_size):
            offset = raw + cli - start + 16
            flags = struct.unpack_from('<I', data, offset)[0]
            struct.pack_into('<I', data, offset, flags & ~2)
            target.write_bytes(data)
            return
    raise ValueError('No CLR header found')


def run(args):
    refs = args.game_assemblies.resolve()
    compiler = args.dotnet or shutil.which('dotnet')
    if not compiler:
        raise RuntimeError('The host tests need the .NET 8 SDK (use --dotnet PATH).')
    compiler = str(Path(compiler).resolve())
    with TemporaryDirectory(prefix='arcana-magic-tests-') as temporary:
        folder = Path(temporary)
        command = folder / 'UnlockCommand.cs'
        command.write_text('using System;\nnamespace SupernaturalExpanded.Journal {\n' + cs_source.class_text(ROOT, 'SpellUnlockCommand') + '\n}')
        sources = [cs_source.file_path(ROOT, 'SpellCatalog'), cs_source.file_path(ROOT, 'SpellKnowledgeSkill'), command,
                   ROOT / 'tools/tests/SpellKnowledgeTests.cs']
        adapter = folder / 'AdapterHost.dll'
        host_copy(args.assembly.resolve(), adapter)
        items = ['<Compile Include="' + escape(str(path), {'"': '&quot;'}) + '" />' for path in sources]
        for name in ('Sims3GameplaySystems', 'Sims3GameplayObjects', 'Sims3MetaData', 'SimIFace', 'UI'):
            path = refs / (name + '.dll')
            if not path.is_file():
                raise ValueError('Missing game reference: ' + str(path))
            items.append('<Reference Include="' + name + '"><HintPath>' + escape(str(path)) + '</HintPath></Reference>')
        (folder / 'Tests.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
            '<UseSharedCompilation>false</UseSharedCompilation><NoWarn>1701</NoWarn></PropertyGroup><ItemGroup>'
            + ''.join(items) + '</ItemGroup></Project>')
        (folder / 'NuGet.Config').write_text('<configuration><packageSources><clear /></packageSources></configuration>')
        env = dict(os.environ, DOTNET_CLI_TELEMETRY_OPTOUT='1', DOTNET_NOLOGO='1')
        subprocess.run([compiler, 'run', '--configuration', 'Release', '--project', str(folder / 'Tests.csproj'), '--', str(adapter)],
                       env=env, check=True)
        support = folder / 'RegressionSupport.cs'
        support.write_text('using System; namespace SupernaturalExpanded.Journal {\n' +
            '\n'.join(cs_source.class_text(ROOT, name) for name in ('Rank', 'RankState', 'WitchProgression')) + '\n}')
        english = folder / 'ArcanaEnglish.g.cs'
        localization.generate(english)
        regression_sources = [support, english, cs_source.file_path(ROOT, 'SpellCatalog'), cs_source.file_path(ROOT, 'WitchCastingRules'),
            cs_source.file_path(ROOT, 'JournalText'), ROOT / 'tools/tests/WitchCastingRulesTests.cs']
        references = [item for item in items if item.startswith('<Reference')]
        (folder / 'Regression.csproj').write_text('<Project Sdk="Microsoft.NET.Sdk"><PropertyGroup><OutputType>Exe</OutputType>'
            '<TargetFramework>net8.0</TargetFramework><EnableDefaultCompileItems>false</EnableDefaultCompileItems>'
            '<UseSharedCompilation>false</UseSharedCompilation><NoWarn>1701</NoWarn></PropertyGroup><ItemGroup>' +
            ''.join('<Compile Include="' + escape(str(path), {'"': '&quot;'}) + '" />' for path in regression_sources) +
            ''.join(references) + '</ItemGroup></Project>')
        subprocess.run([compiler, 'run', '--configuration', 'Release', '--project', str(folder / 'Regression.csproj')], env=env, check=True)


if __name__ == '__main__':
    parser = argparse.ArgumentParser(description="Run the host-side Spell Knowledge and casting tests with the .NET 8 SDK.")
    parser.add_argument('--dotnet')
    parser.add_argument('--game-assemblies', type=Path, default=ROOT / '.deps/Sims3')
    parser.add_argument('--assembly', type=Path, default=ROOT / 'build/Arcana - An Occult Overhaul.dll')
    run(parser.parse_args())
