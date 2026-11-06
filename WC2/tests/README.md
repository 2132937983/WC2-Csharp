# Scripted smoke tests

Each `*.txt` file is a script for the built-in input driver (`WC2/Host/SmokeTest.cs`): coordinates are
logical screen units (568x320 phone layout). `shot NAME` saves a screenshot next to the `--screenshot`
path. Run them all with `tests\run-tests.ps1` after `dotnet build`; results go to `tests\out`.
