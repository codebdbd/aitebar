# UI Contract

## Locked Layout Invariants

1. `SettingsWindow`: высота 2-й и 3-й колонок должна быть строго одинаковой всегда.
2. Трогать высоту 2-й и 3-й колонок запрещено: нельзя задавать им разные `Height`, `MinHeight` или `MaxHeight`.
3. Синхронизацию высоты 3-й колонки с 2-й запрещено менять или удалять:
   - `AiteBar/SettingsWindow.xaml`
   - `Grid Grid.Column="2" ... Height="{Binding ElementName=MiddleColumnGrid, Path=ActualHeight}"`
4. Любые правки `SettingsWindow.xaml` обязаны сохранять этот инвариант без исключений.
