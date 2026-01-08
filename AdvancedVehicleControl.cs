using GTA;
using GTA.Math;
using GTA.Native;
using System;
using System.Drawing;
using System.IO;
using System.Net;
using System.Windows.Forms;
using GTAControl = GTA.Control;
using Newtonsoft.Json.Linq;

public class AdvancedVehicleControl : Script
{
    // ================= НАСТРОЙКИ КЛАВИШ (по умолчанию) =================
    private Keys KeyEngine = Keys.Y;
    private Keys KeyRefuel = Keys.E;
    private Keys KeySeatbelt = Keys.G;
    private Keys KeyABS = Keys.H;
    private Keys KeyHazard = Keys.Up;
    private Keys KeyLeftIndicator = Keys.Right;
    private Keys KeyRightIndicator = Keys.Left;
    private Keys KeyMenu = Keys.F10;
    private Keys KeyCancelRefuel = Keys.F;

    // ================= СОСТОЯНИЕ ТРАНСПОРТА =================
    private Vehicle vehicle;
    private bool engineOn = true;
    private bool seatbelt = false;
    private bool abs = true;

    // ================= ТОПЛИВО =================
    private float fuel = 100f;
    private const float MaxFuel = 100f;
    private const float FuelPerKm = 0.8f;
    private Vector3 lastPos;

    // ================= ЗАПРАВКА =================
    private bool isRefueling = false;
    private bool isNearGasStation = false;
    private Vector3 currentGasStation;
    private float fuelPrice = 1.5f;
    private bool isGasMarkerVisible = false;

    // Массив для хранения иконок на мини-карте
    private Blip[] gasStationBlips;

    // Настройки маркеров заправки
    private float markerSize = 2.0f;
    private Color markerColor = Color.FromArgb(150, 0, 255, 0);

    private readonly Vector3[] GasStations =
    {
        new Vector3(265.6f, -1261.3f, 29.3f),
        new Vector3(819.6f, -1028.8f, 26.4f),
        new Vector3(1208.9f, -1402.5f, 35.2f),
        new Vector3(-72.9f, -1764.1f, 29.5f),
        new Vector3(-2554.9f, 2334.2f, 33.1f),
        new Vector3(49.4f, 2778.8f, 58.0f),
        new Vector3(263.9f, 2606.5f, 44.9f),
        new Vector3(1207.3f, 2660.1f, 37.9f),
        new Vector3(2539.7f, 2594.2f, 37.9f),
        new Vector3(2679.9f, 3264.9f, 55.2f),
        new Vector3(2005.9f, 3774.3f, 32.2f),
        new Vector3(1687.9f, 4929.4f, 42.1f),
        new Vector3(1701.3f, 6416.1f, 32.8f),
        new Vector3(179.9f, 6603.1f, 31.9f),
        new Vector3(-94.4f, 6419.6f, 31.5f),
        new Vector3(-2096.6f, -320.3f, 13.0f),
        new Vector3(-724.6f, -935.0f, 19.0f),
        new Vector3(-1437.5f, -276.5f, 46.2f),
        new Vector3(-70.2f, -1761.8f, 29.5f)
    };

    // ================= ПОВОРОТНИКИ =================
    private bool hazard, leftIndicator, rightIndicator;
    private bool blink;
    private int blinkTimer;

    // ================= НАСТРОЙКИ HUD =================
    private float fuelBarX = 0.08f;
    private float fuelBarY = 0.88f;
    private float fuelBarWidth = 0.15f;
    private float fuelBarHeight = 0.012f;

    // Отдельные настройки для текста (не привязаны к полоске)
    private float textFuelX = 0.005f;
    private float textFuelY = 0.865f;
    private float textEngineX = 0.055f;
    private float textEngineY = 0.865f;
    private float textABSX = 0.095f;
    private float textABSY = 0.865f;
    private float textBeltX = 0.135f;
    private float textBeltY = 0.865f;

    private float textScale = 0.30f;
    private Color fuelColor = Color.FromArgb(0, 255, 255);
    private Color textColor = Color.Orange;
    private Color seatbeltColor = Color.Cyan;

    // Для редактирования
    private bool editingFuelBar = false;
    private bool editingText = false;
    private bool editingMarker = false;
    private int textEditMode = 0; // 0-Fuel, 1-Engine, 2-ABS, 3-Belt
    private int markerEditMode = 0; // 0-Размер, 1-Цвет

    // Для меню
    private bool showMenu = false;
    private int menuSelection = 0;
    private bool editingKeys = false;
    private int keySelection = 0;
    private bool waitingForKey = false;

    // Элементы меню
    private readonly string[] mainMenuItems = {
        "Редактировать полоску топлива",
        "Редактировать текст",
        "Настройка маркеров заправки",
        "Изменить цвет полоски",
        "Настройка клавиш",
        "Проверить обновления",
        "Сохранить настройки",
        "Сбросить настройки"
    };

    private readonly string[] keyMenuItems = {
        "Двигатель (Y)",
        "Заправка (E)",
        "Ремни (G)",
        "ABS (H)",
        "Аварийка (Up)",
        "Левый поворотник (Right)",
        "Правый поворотник (Left)",
        "Меню (F10)",
        "Отмена заправки (F)",
        "Назад"
    };

    private readonly string[] markerMenuItems = {
        "Изменить размер маркера",
        "Изменить цвет маркера",
        "Назад"
    };

    // Для системы обновлений
    private string currentVersion = "2.0.0";
    private string latestVersion = "";
    private bool updateAvailable = false;
    private string updateUrl = "https://github.com/AlexFerguson/AdvancedVehicleControl/releases/latest";
    private bool updateChecked = false;
    private const string GITHUB_API_URL = "https://api.github.com/repos/AlexFerguson/AdvancedVehicleControl/releases/latest";

    // Для системы логов
    private string logFilePath;
    private bool debugEnabled = true;

    public AdvancedVehicleControl()
    {
        Tick += OnTick;
        KeyDown += OnKeyDown;
        KeyUp += OnKeyUp;
        Interval = 0;

        // Инициализация системы логов
        logFilePath = Path.Combine(AppDomain.CurrentDomain.BaseDirectory, "scripts", "AdvancedVehicleControl.log");
        LogToFile("=== Advanced Vehicle Control Started ===");
        LogToFile($"Script version: {currentVersion}");
        LogToFile($"Game version: {Game.Version}");
        LogToFile($"Date: {DateTime.Now}");

        try
        {
            LoadSettings();
            CreateGasStationBlips();
            LogToFile("Script initialized successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"Error during initialization: {ex.Message}\n{ex.StackTrace}");
            ShowNotification("~r~Ошибка загрузки скрипта!");
        }
    }

    // ================= СИСТЕМА ЛОГОВ =================
    private void LogToFile(string message)
    {
        if (!debugEnabled) return;

        try
        {
            string timestamp = DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff");
            string logMessage = $"[{timestamp}] {message}";

            // Создаем директорию если не существует
            string logDir = Path.GetDirectoryName(logFilePath);
            if (!Directory.Exists(logDir))
            {
                Directory.CreateDirectory(logDir);
            }

            File.AppendAllText(logFilePath, logMessage + Environment.NewLine);
        }
        catch (Exception ex)
        {
            // Нельзя логировать ошибку логирования :)
        }
    }

    // ================= СИСТЕМА ОБНОВЛЕНИЙ =================
    private void CheckForUpdates()
    {
        try
        {
            LogToFile("Checking for updates...");

            using (WebClient client = new WebClient())
            {
                client.Headers.Add("User-Agent", "AdvancedVehicleControl-GTAV-Mod");
                string json = client.DownloadString(GITHUB_API_URL);
                JObject release = JObject.Parse(json);

                latestVersion = release["tag_name"]?.ToString().Replace("v", "").Trim();

                if (!string.IsNullOrEmpty(latestVersion))
                {
                    LogToFile($"Latest version on GitHub: {latestVersion}");

                    Version current = new Version(currentVersion);
                    Version latest = new Version(latestVersion);

                    if (latest > current)
                    {
                        updateAvailable = true;
                        updateUrl = release["html_url"]?.ToString();
                        LogToFile($"Update available: {latestVersion}");
                        ShowNotification($"~y~Доступно обновление v{latestVersion}!");
                    }
                    else
                    {
                        updateAvailable = false;
                        LogToFile("Script is up to date");
                        ShowNotification("~g~Версия актуальна!");
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Update check failed: {ex.Message}");
            ShowNotification("~r~Ошибка проверки обновлений!");
        }
    }

    // ================= СОЗДАНИЕ ИКОНОК НА МИНИ-КАРТЕ =================
    private void CreateGasStationBlips()
    {
        try
        {
            LogToFile($"Creating gas station blips ({GasStations.Length} stations)");
            gasStationBlips = new Blip[GasStations.Length];

            for (int i = 0; i < GasStations.Length; i++)
            {
                try
                {
                    // Создаем иконку на карте
                    Blip blip = World.CreateBlip(GasStations[i]);

                    // Настраиваем внешний вид иконки
                    blip.Sprite = BlipSprite.JerryCan;
                    blip.Color = BlipColor.Yellow;
                    blip.Scale = 0.7f;
                    blip.IsShortRange = true;
                    blip.Name = "Заправка";

                    // Сохраняем иконку в массив
                    gasStationBlips[i] = blip;
                }
                catch (Exception ex)
                {
                    LogToFile($"Error creating blip {i}: {ex.Message}");
                }
            }

            LogToFile("Gas station blips created successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"Error creating gas station blips: {ex.Message}");
            ShowNotification("~r~Ошибка создания иконок заправок");
        }
    }

    // ================= ОСНОВНОЙ ЦИКЛ =================
    private void OnTick(object sender, EventArgs e)
    {
        try
        {
            Ped p = Game.Player.Character;

            CheckGasStations();

            if (!p.IsInVehicle())
            {
                if (vehicle != null)
                {
                    vehicle = null;
                    lastPos = Vector3.Zero;
                    hazard = false;
                    leftIndicator = false;
                    rightIndicator = false;
                }

                if (isNearGasStation)
                {
                    DrawRefuelMarker();
                }
                return;
            }

            vehicle = p.CurrentVehicle;
            if (!vehicle.Exists()) return;

            HandleFuel();
            HandleBrakeLights();
            HandleIndicators();
            HandleABS();
            DrawHUD();

            if (!isRefueling)
            {
                DrawGasMarker();
            }
            else
            {
                HandleRefuel();
            }

            if (editingFuelBar) HandleFuelBarEdit();
            if (editingText) HandleTextEdit();
            if (editingMarker) HandleMarkerEdit();

            if (showMenu) DrawMenu();
        }
        catch (Exception ex)
        {
            LogToFile($"Error in OnTick: {ex.Message}\n{ex.StackTrace}");
        }
    }

    // ================= ПРОВЕРКА ЗАПРАВОК =================
    private void CheckGasStations()
    {
        try
        {
            Ped p = Game.Player.Character;
            Vector3 playerPos = p.Position;
            isNearGasStation = false;

            foreach (var gasPos in GasStations)
            {
                float distance = playerPos.DistanceTo(gasPos);

                if (distance < 3.5f)
                {
                    isNearGasStation = true;
                    currentGasStation = gasPos;
                    isGasMarkerVisible = true;
                    return;
                }
                else if (distance < 15f)
                {
                    isGasMarkerVisible = true;
                    currentGasStation = gasPos;
                }
            }

            if (!isNearGasStation)
            {
                isGasMarkerVisible = false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in CheckGasStations: {ex.Message}");
        }
    }

    // ================= МАРКЕР ДЛЯ ЗАПРАВКИ =================
    private void DrawRefuelMarker()
    {
        if (!isGasMarkerVisible) return;

        try
        {
            // Зеленый маркер для позиции заправки
            Vector3 markerPos = currentGasStation;

            // Основной маркер
            World.DrawMarker(
                MarkerType.VerticalCylinder,
                markerPos + new Vector3(0, 0, 0.05f),
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(markerSize, markerSize, 0.2f),
                markerColor
            );

            // Столбы заправки
            World.DrawMarker(
                MarkerType.ChevronUpx1,
                markerPos + new Vector3(0, 0, 2.5f),
                Vector3.Zero,
                new Vector3(0, 0, 0),
                new Vector3(1.5f, 1.5f, 2.0f),
                Color.FromArgb(200, 255, 255, 0)
            );

            // Подсветка области
            for (int i = 0; i < 8; i++)
            {
                float angle = i * 45f;
                Vector3 offset = new Vector3(
                    (float)Math.Cos(angle * Math.PI / 180) * 2f,
                    (float)Math.Sin(angle * Math.PI / 180) * 2f,
                    0.1f
                );

                World.DrawMarker(
                    MarkerType.ThickChevronUp,
                    markerPos + offset,
                    Vector3.Zero,
                    new Vector3(0, 0, angle),
                    new Vector3(0.5f, 0.5f, 0.1f),
                    Color.FromArgb(100, 255, 255, 0)
                );
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawRefuelMarker: {ex.Message}");
        }
    }

    // ================= ТОПЛИВО =================
    private void HandleFuel()
    {
        try
        {
            if (!engineOn || vehicle.Speed < 1f) return;

            if (lastPos == Vector3.Zero)
            {
                lastPos = vehicle.Position;
                return;
            }

            float dist = vehicle.Position.DistanceTo(lastPos);
            fuel -= (dist / 1000f) * FuelPerKm;
            fuel = Math.Max(0f, fuel);

            if (fuel <= 0f)
            {
                vehicle.IsEngineRunning = false;
                engineOn = false;
                ShowNotification("Кончилось топливо!");
            }

            lastPos = vehicle.Position;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleFuel: {ex.Message}");
        }
    }

    // ================= ЛОГИКА СТОП-СИГНАЛОВ =================
    private void HandleBrakeLights()
    {
        try
        {
            bool brakePressed = Game.IsControlPressed(GTAControl.VehicleBrake);
            bool handbrakePressed = Game.IsControlPressed(GTAControl.VehicleHandbrake);
            bool stopped = vehicle.Speed < 0.5f;

            bool brakeLightsOn = brakePressed || handbrakePressed || stopped;
            Function.Call(Hash.SET_VEHICLE_BRAKE_LIGHTS, vehicle, brakeLightsOn);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleBrakeLights: {ex.Message}");
        }
    }

    // ================= ABS =================
    private void HandleABS()
    {
        try
        {
            if (!abs) return;

            if (Game.IsControlPressed(GTAControl.VehicleBrake) && vehicle.Speed > 15f)
            {
                float brakePower = Game.GetControlValueNormalized(GTAControl.VehicleBrake);
                if (brakePower > 0.8f)
                {
                    Game.SetControlValueNormalized(GTAControl.VehicleBrake, 0.8f);
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleABS: {ex.Message}");
        }
    }

    // ================= ПОВОРОТНИКИ =================
    private void HandleIndicators()
    {
        try
        {
            if (Game.GameTime - blinkTimer > 500)
            {
                blink = !blink;
                blinkTimer = Game.GameTime;
            }

            bool leftIndicatorOn = hazard || (leftIndicator && blink);
            bool rightIndicatorOn = hazard || (rightIndicator && blink);

            Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, vehicle, 0, leftIndicatorOn);
            Function.Call(Hash.SET_VEHICLE_INDICATOR_LIGHTS, vehicle, 1, rightIndicatorOn);

            if (vehicle.Speed > 10f && (leftIndicator || rightIndicator))
            {
                float steering = vehicle.SteeringAngle;

                if ((steering < -0.1f && rightIndicator) || (steering > 0.1f && leftIndicator))
                {
                    if (Game.GameTime % 2000 < 100)
                    {
                        leftIndicator = false;
                        rightIndicator = false;
                    }
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleIndicators: {ex.Message}");
        }
    }

    // ================= ЗАПРАВКА =================
    private void TryRefuel()
    {
        try
        {
            if (isRefueling)
            {
                StopRefuel();
                return;
            }

            if (!isNearGasStation)
            {
                ShowNotification("Встаньте на зеленый маркер возле заправки!");
                return;
            }

            if (fuel >= MaxFuel)
            {
                ShowNotification("Бак уже полный!");
                return;
            }

            isRefueling = true;
            ShowNotification("Заправка... Держите E для продолжения. Нажмите F для отмены");
        }
        catch (Exception ex)
        {
            LogToFile($"Error in TryRefuel: {ex.Message}");
        }
    }

    private void HandleRefuel()
    {
        try
        {
            if (!isRefueling) return;

            if (!isNearGasStation)
            {
                ShowNotification("Отъехали от заправки!");
                StopRefuel();
                return;
            }

            if (Game.IsControlPressed(GTAControl.Context))
            {
                if (fuel < MaxFuel && Game.Player.Money > 0)
                {
                    float fuelToAdd = 0.5f;
                    float cost = fuelToAdd * fuelPrice;

                    if (Game.Player.Money >= (int)cost)
                    {
                        fuel = Math.Min(MaxFuel, fuel + fuelToAdd);
                        Game.Player.Money -= (int)cost;

                        if (Math.Abs(fuel % 5) < 0.3f)
                        {
                            ShowNotification($"Заправка... {fuel:0}%");
                        }
                    }
                    else
                    {
                        ShowNotification("Недостаточно денег!");
                        StopRefuel();
                    }
                }

                if (fuel >= MaxFuel)
                {
                    ShowNotification("Бак полон!");
                    StopRefuel();
                }
            }

            if (Game.IsKeyPressed(KeyCancelRefuel))
            {
                StopRefuel();
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleRefuel: {ex.Message}");
            StopRefuel();
        }
    }

    private void StopRefuel()
    {
        try
        {
            isRefueling = false;
            ShowNotification("Заправка остановлена");
        }
        catch (Exception ex)
        {
            LogToFile($"Error in StopRefuel: {ex.Message}");
            isRefueling = false;
        }
    }

    // ================= МАРКЕРЫ АЗС =================
    private void DrawGasMarker()
    {
        if (!isGasMarkerVisible) return;

        try
        {
            World.DrawMarker(
                MarkerType.VerticalCylinder,
                currentGasStation + new Vector3(0, 0, 0.1f),
                Vector3.Zero,
                Vector3.Zero,
                new Vector3(2.5f, 2.5f, 0.5f),
                Color.FromArgb(100, 255, 255, 0)
            );
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawGasMarker: {ex.Message}");
        }
    }

    // ================= HUD =================
    private void DrawHUD()
    {
        try
        {
            float fuelPercent = fuel / MaxFuel;
            float barWidth = fuelBarWidth * fuelPercent;

            // Фон полоски топлива
            Function.Call(Hash.DRAW_RECT,
                fuelBarX, fuelBarY,
                fuelBarWidth, fuelBarHeight,
                30, 30, 30, 200);

            // Сама полоска топлива
            if (barWidth > 0)
            {
                Function.Call(Hash.DRAW_RECT,
                    fuelBarX - fuelBarWidth / 2 + barWidth / 2, fuelBarY,
                    barWidth, fuelBarHeight * 0.8f,
                    fuelColor.R, fuelColor.G, fuelColor.B, 255);
            }

            // Текстовая информация (ОТДЕЛЬНО ОТ ПОЛОСКИ)
            DrawText($"ТОПЛИВО: {fuel:0}%", textFuelX, textFuelY, textColor);
            DrawText("ДВИГ", textEngineX, textEngineY, engineOn ? Color.Lime : Color.Red);
            DrawText("ABS", textABSX, textABSY, abs ? Color.Lime : Color.Red);
            DrawText("РЕМНИ", textBeltX, textBeltY, seatbelt ? seatbeltColor : Color.Red);

            // Инструкции при редактировании
            if (editingFuelBar)
            {
                DrawScreenText("←→↑↓ - Перемещение полоски", 0.02f, 0.02f, 0.25f, Color.Yellow);
                DrawScreenText("W/S - Высота, A/D - Ширина", 0.02f, 0.05f, 0.25f, Color.Yellow);
                DrawScreenText("F10 - Сохранить и выйти", 0.02f, 0.08f, 0.25f, Color.Yellow);
            }

            if (editingText)
            {
                string currentText = textEditMode == 0 ? "ТОПЛИВО" :
                                   textEditMode == 1 ? "ДВИГ" :
                                   textEditMode == 2 ? "ABS" : "РЕМНИ";
                DrawScreenText($"Редактирование: {currentText}", 0.02f, 0.02f, 0.25f, Color.Yellow);
                DrawScreenText("←→↑↓ - Перемещение текста", 0.02f, 0.05f, 0.25f, Color.Yellow);
                DrawScreenText("+/- - Размер текста", 0.02f, 0.08f, 0.25f, Color.Yellow);
                DrawScreenText("TAB - Выбор текста", 0.02f, 0.11f, 0.25f, Color.Yellow);
                DrawScreenText("F10 - Сохранить и выйти", 0.02f, 0.14f, 0.25f, Color.Yellow);
            }

            if (editingMarker)
            {
                string currentMode = markerEditMode == 0 ? "РАЗМЕР" : "ЦВЕТ";
                DrawScreenText($"Редактирование маркера: {currentMode}", 0.02f, 0.02f, 0.25f, Color.Yellow);
                DrawScreenText("W/S - Увеличить/Уменьшить размер", 0.02f, 0.05f, 0.25f, Color.Yellow);
                DrawScreenText("C - Сменить цвет", 0.02f, 0.08f, 0.25f, Color.Yellow);
                DrawScreenText("F10 - Сохранить и выйти", 0.02f, 0.11f, 0.25f, Color.Yellow);
            }

            // Уведомление об обновлении
            if (updateAvailable)
            {
                DrawScreenText($"Доступно обновление v{latestVersion}!", 0.02f, 0.17f, 0.25f, Color.Yellow);
                DrawScreenText("Нажмите F11 для подробностей", 0.02f, 0.20f, 0.25f, Color.Yellow);
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawHUD: {ex.Message}");
        }
    }

    // ================= РЕДАКТИРОВАНИЕ ПОЛОСКИ ТОПЛИВА =================
    private void HandleFuelBarEdit()
    {
        try
        {
            float moveSpeed = 0.001f;

            if (Game.IsKeyPressed(Keys.Left))
                fuelBarX -= moveSpeed;
            else if (Game.IsKeyPressed(Keys.Right))
                fuelBarX += moveSpeed;
            else if (Game.IsKeyPressed(Keys.Up))
                fuelBarY -= moveSpeed;
            else if (Game.IsKeyPressed(Keys.Down))
                fuelBarY += moveSpeed;

            fuelBarX = Math.Max(0.05f, Math.Min(0.95f, fuelBarX));
            fuelBarY = Math.Max(0.05f, Math.Min(0.95f, fuelBarY));

            float sizeSpeed = 0.001f;
            if (Game.IsKeyPressed(Keys.W))
                fuelBarHeight = Math.Min(0.05f, fuelBarHeight + sizeSpeed);
            else if (Game.IsKeyPressed(Keys.S))
                fuelBarHeight = Math.Max(0.005f, fuelBarHeight - sizeSpeed);
            else if (Game.IsKeyPressed(Keys.A))
                fuelBarWidth = Math.Max(0.05f, fuelBarWidth - sizeSpeed * 5);
            else if (Game.IsKeyPressed(Keys.D))
                fuelBarWidth = Math.Min(0.5f, fuelBarWidth + sizeSpeed * 5);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleFuelBarEdit: {ex.Message}");
        }
    }

    // ================= РЕДАКТИРОВАНИЕ ТЕКСТА =================
    private void HandleTextEdit()
    {
        try
        {
            // Переключение между текстами TAB
            if (Game.IsKeyPressed(Keys.Tab))
            {
                textEditMode = (textEditMode + 1) % 4;
                ShowNotification($"Редактирование: {(textEditMode == 0 ? "ТОПЛИВО" : textEditMode == 1 ? "ДВИГ" : textEditMode == 2 ? "ABS" : "РЕМНИ")}");
            }

            float moveSpeed = 0.001f;

            // Перемещение выбранного текста
            switch (textEditMode)
            {
                case 0: // ТОПЛИВО
                    if (Game.IsKeyPressed(Keys.Left)) textFuelX -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Right)) textFuelX += moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Up)) textFuelY -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Down)) textFuelY += moveSpeed;
                    break;

                case 1: // ДВИГ
                    if (Game.IsKeyPressed(Keys.Left)) textEngineX -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Right)) textEngineX += moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Up)) textEngineY -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Down)) textEngineY += moveSpeed;
                    break;

                case 2: // ABS
                    if (Game.IsKeyPressed(Keys.Left)) textABSX -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Right)) textABSX += moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Up)) textABSY -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Down)) textABSY += moveSpeed;
                    break;

                case 3: // РЕМНИ
                    if (Game.IsKeyPressed(Keys.Left)) textBeltX -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Right)) textBeltX += moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Up)) textBeltY -= moveSpeed;
                    else if (Game.IsKeyPressed(Keys.Down)) textBeltY += moveSpeed;
                    break;
            }

            // Ограничиваем позиции
            textFuelX = Math.Max(0.001f, Math.Min(0.999f, textFuelX));
            textFuelY = Math.Max(0.001f, Math.Min(0.999f, textFuelY));
            textEngineX = Math.Max(0.001f, Math.Min(0.999f, textEngineX));
            textEngineY = Math.Max(0.001f, Math.Min(0.999f, textEngineY));
            textABSX = Math.Max(0.001f, Math.Min(0.999f, textABSX));
            textABSY = Math.Max(0.001f, Math.Min(0.999f, textABSY));
            textBeltX = Math.Max(0.001f, Math.Min(0.999f, textBeltX));
            textBeltY = Math.Max(0.001f, Math.Min(0.999f, textBeltY));

            // Изменение размера текста (работает для всех текстов)
            if (Game.IsKeyPressed(Keys.Add) || Game.IsKeyPressed(Keys.Oemplus))
                textScale = Math.Min(1.0f, textScale + 0.01f);
            else if (Game.IsKeyPressed(Keys.Subtract) || Game.IsKeyPressed(Keys.OemMinus))
                textScale = Math.Max(0.1f, textScale - 0.01f);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleTextEdit: {ex.Message}");
        }
    }

    // ================= РЕДАКТИРОВАНИЕ МАРКЕРА =================
    private void HandleMarkerEdit()
    {
        try
        {
            if (Game.IsKeyPressed(Keys.Tab))
            {
                markerEditMode = (markerEditMode + 1) % 2;
                ShowNotification($"Режим редактирования: {(markerEditMode == 0 ? "РАЗМЕР" : "ЦВЕТ")}");
            }

            if (markerEditMode == 0) // Редактирование размера
            {
                float sizeSpeed = 0.1f;
                if (Game.IsKeyPressed(Keys.W))
                    markerSize = Math.Min(5.0f, markerSize + sizeSpeed);
                else if (Game.IsKeyPressed(Keys.S))
                    markerSize = Math.Max(0.5f, markerSize - sizeSpeed);
            }
            else if (markerEditMode == 1) // Редактирование цвета
            {
                if (Game.IsKeyPressed(Keys.C))
                    CycleMarkerColor();
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleMarkerEdit: {ex.Message}");
        }
    }

    private void CycleMarkerColor()
    {
        if (markerColor == Color.FromArgb(150, 0, 255, 0)) // Зеленый
            markerColor = Color.FromArgb(150, 0, 0, 255);  // Синий
        else if (markerColor == Color.FromArgb(150, 0, 0, 255))
            markerColor = Color.FromArgb(150, 255, 0, 0);  // Красный
        else if (markerColor == Color.FromArgb(150, 255, 0, 0))
            markerColor = Color.FromArgb(150, 255, 255, 0); // Желтый
        else if (markerColor == Color.FromArgb(150, 255, 255, 0))
            markerColor = Color.FromArgb(150, 255, 0, 255); // Пурпурный
        else
            markerColor = Color.FromArgb(150, 0, 255, 0);   // Вернуться к зеленому

        string colorName = markerColor == Color.FromArgb(150, 0, 255, 0) ? "Зеленый" :
                          markerColor == Color.FromArgb(150, 0, 0, 255) ? "Синий" :
                          markerColor == Color.FromArgb(150, 255, 0, 0) ? "Красный" :
                          markerColor == Color.FromArgb(150, 255, 255, 0) ? "Желтый" : "Пурпурный";
        ShowNotification($"Цвет маркера изменен на {colorName}");
    }

    // ================= МЕНЮ =================
    private void DrawMenu()
    {
        try
        {
            if (editingKeys)
            {
                DrawKeyMenu();
            }
            else if (editingMarker)
            {
                DrawMarkerMenu();
            }
            else
            {
                DrawMainMenu();
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawMenu: {ex.Message}");
        }
    }

    private void DrawMainMenu()
    {
        float menuX = 0.5f;
        float menuY = 0.25f;
        float itemHeight = 0.045f;
        float menuWidth = 0.28f;

        // Фон меню
        Function.Call(Hash.DRAW_RECT,
            menuX, menuY + (mainMenuItems.Length * itemHeight) / 2,
            menuWidth, mainMenuItems.Length * itemHeight,
            0, 0, 0, 200);

        // Заголовок
        DrawScreenText($"НАСТРОЙКИ HUD v{currentVersion}", menuX, menuY - 0.06f, 0.35f, Color.White);

        // Элементы меню
        for (int i = 0; i < mainMenuItems.Length; i++)
        {
            float itemY = menuY + (i * itemHeight);
            Color itemColor = (i == menuSelection) ? Color.Yellow : Color.White;

            if (i == menuSelection)
            {
                Function.Call(Hash.DRAW_RECT,
                    menuX, itemY + itemHeight / 2,
                    menuWidth - 0.01f, itemHeight - 0.01f,
                    100, 100, 0, 100);
            }

            DrawScreenText(mainMenuItems[i], menuX - 0.13f, itemY, 0.28f, itemColor);
        }

        // Инструкции
        DrawScreenText("Стрелки - Выбор, Enter - Принять, F10 - Выход",
            0.5f, menuY + (mainMenuItems.Length * itemHeight) + 0.03f,
            0.22f, Color.Cyan);
    }

    private void DrawKeyMenu()
    {
        float menuX = 0.5f;
        float menuY = 0.3f;
        float itemHeight = 0.045f;
        float menuWidth = 0.3f;

        // Фон меню
        Function.Call(Hash.DRAW_RECT,
            menuX, menuY + (keyMenuItems.Length * itemHeight) / 2,
            menuWidth, keyMenuItems.Length * itemHeight,
            0, 0, 0, 200);

        // Заголовок
        DrawScreenText("НАСТРОЙКА КЛАВИШ", menuX, menuY - 0.06f, 0.4f, Color.White);

        UpdateKeyMenuText();

        for (int i = 0; i < keyMenuItems.Length; i++)
        {
            float itemY = menuY + (i * itemHeight);
            Color itemColor = (i == keySelection) ? Color.Yellow : Color.White;

            if (i == keySelection)
            {
                Function.Call(Hash.DRAW_RECT,
                    menuX, itemY + itemHeight / 2,
                    menuWidth - 0.01f, itemHeight - 0.01f,
                    100, 100, 0, 100);
            }

            DrawScreenText(keyMenuItems[i], menuX - 0.14f, itemY, 0.28f, itemColor);
        }

        if (waitingForKey)
        {
            DrawScreenText("Нажмите новую клавишу... Esc - Отмена",
                0.5f, menuY + (keyMenuItems.Length * itemHeight) + 0.03f,
                0.25f, Color.Yellow);
        }
        else
        {
            DrawScreenText("Стрелки - Выбор, Enter - Изменить, F10 - Назад",
                0.5f, menuY + (keyMenuItems.Length * itemHeight) + 0.03f,
                0.25f, Color.Cyan);
        }
    }

    private void DrawMarkerMenu()
    {
        float menuX = 0.5f;
        float menuY = 0.3f;
        float itemHeight = 0.045f;
        float menuWidth = 0.3f;

        // Фон меню
        Function.Call(Hash.DRAW_RECT,
            menuX, menuY + (markerMenuItems.Length * itemHeight) / 2,
            menuWidth, markerMenuItems.Length * itemHeight,
            0, 0, 0, 200);

        // Заголовок
        DrawScreenText("НАСТРОЙКА МАРКЕРОВ", menuX, menuY - 0.06f, 0.4f, Color.White);

        for (int i = 0; i < markerMenuItems.Length; i++)
        {
            float itemY = menuY + (i * itemHeight);
            Color itemColor = (i == markerEditMode) ? Color.Yellow : Color.White;

            if (i == markerEditMode)
            {
                Function.Call(Hash.DRAW_RECT,
                    menuX, itemY + itemHeight / 2,
                    menuWidth - 0.01f, itemHeight - 0.01f,
                    100, 100, 0, 100);
            }

            DrawScreenText(markerMenuItems[i], menuX - 0.14f, itemY, 0.28f, itemColor);
        }

        DrawScreenText($"Текущий размер: {markerSize:0.1f}", 0.5f, menuY + (markerMenuItems.Length * itemHeight) + 0.03f, 0.25f, Color.Lime);
        DrawScreenText("TAB - Переключение, W/S - Размер, C - Цвет, F10 - Назад",
            0.5f, menuY + (markerMenuItems.Length * itemHeight) + 0.06f,
            0.22f, Color.Cyan);
    }

    private void UpdateKeyMenuText()
    {
        keyMenuItems[0] = $"Двигатель ({KeyEngine})";
        keyMenuItems[1] = $"Заправка ({KeyRefuel})";
        keyMenuItems[2] = $"Ремни ({KeySeatbelt})";
        keyMenuItems[3] = $"ABS ({KeyABS})";
        keyMenuItems[4] = $"Аварийка ({KeyHazard})";
        keyMenuItems[5] = $"Левый поворотник ({KeyLeftIndicator})";
        keyMenuItems[6] = $"Правый поворотник ({KeyRightIndicator})";
        keyMenuItems[7] = $"Меню ({KeyMenu})";
        keyMenuItems[8] = $"Отмена заправки ({KeyCancelRefuel})";
        keyMenuItems[9] = "Назад";
    }

    private void HandleMenuInput()
    {
        if (waitingForKey)
        {
            return;
        }

        try
        {
            if (Game.IsKeyPressed(Keys.Up))
            {
                if (editingKeys)
                    keySelection--;
                else if (editingMarker)
                    markerEditMode = Math.Max(0, markerEditMode - 1);
                else
                    menuSelection--;

                if (editingKeys && keySelection < 0) keySelection = keyMenuItems.Length - 1;
                if (!editingKeys && !editingMarker && menuSelection < 0) menuSelection = mainMenuItems.Length - 1;
            }
            else if (Game.IsKeyPressed(Keys.Down))
            {
                if (editingKeys)
                    keySelection++;
                else if (editingMarker)
                    markerEditMode = Math.Min(markerMenuItems.Length - 1, markerEditMode + 1);
                else
                    menuSelection++;

                if (editingKeys && keySelection >= keyMenuItems.Length) keySelection = 0;
                if (!editingKeys && !editingMarker && menuSelection >= mainMenuItems.Length) menuSelection = 0;
            }
            else if (Game.IsKeyPressed(Keys.Enter))
            {
                ExecuteMenuAction();
            }
            else if (Game.IsKeyPressed(Keys.F11) && updateAvailable)
            {
                ShowUpdateDetails();
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in HandleMenuInput: {ex.Message}");
        }
    }

    private void ExecuteMenuAction()
    {
        try
        {
            if (editingKeys)
            {
                if (keySelection == 9) // Назад
                {
                    editingKeys = false;
                    keySelection = 0;
                }
                else
                {
                    waitingForKey = true;
                    ShowNotification("Нажмите новую клавишу... Esc для отмены");
                }
            }
            else if (editingMarker)
            {
                if (markerEditMode == 2) // Назад
                {
                    editingMarker = false;
                    markerEditMode = 0;
                }
            }
            else
            {
                switch (menuSelection)
                {
                    case 0: // Редактировать полоску топлива
                        editingFuelBar = true;
                        editingText = false;
                        editingMarker = false;
                        showMenu = false;
                        ShowNotification("Режим редактирования полоски топлива");
                        break;

                    case 1: // Редактировать текст
                        editingText = true;
                        editingFuelBar = false;
                        editingMarker = false;
                        textEditMode = 0;
                        showMenu = false;
                        ShowNotification("Режим редактирования текста (TAB - переключение)");
                        break;

                    case 2: // Настройка маркеров заправки
                        editingMarker = true;
                        editingFuelBar = false;
                        editingText = false;
                        markerEditMode = 0;
                        showMenu = false;
                        ShowNotification("Режим редактирования маркеров");
                        break;

                    case 3: // Изменить цвет полоски
                        CycleFuelColor();
                        break;

                    case 4: // Настройка клавиш
                        editingKeys = true;
                        editingFuelBar = false;
                        editingText = false;
                        editingMarker = false;
                        keySelection = 0;
                        UpdateKeyMenuText();
                        break;

                    case 5: // Проверить обновления
                        CheckForUpdates();
                        break;

                    case 6: // Сохранить настройки
                        SaveSettings();
                        showMenu = false;
                        ShowNotification("Настройки сохранены!");
                        break;

                    case 7: // Сбросить настройки
                        ResetSettings();
                        showMenu = false;
                        ShowNotification("Настройки сброшены к стандартным");
                        break;
                }
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in ExecuteMenuAction: {ex.Message}");
        }
    }

    private void ShowUpdateDetails()
    {
        try
        {
            ShowNotification($"~y~Текущая версия: ~w~v{currentVersion}");
            ShowNotification($"~y~Доступна версия: ~w~v{latestVersion}");
            ShowNotification("~g~Перейдите на GitHub для обновления!");

            // Можно открыть браузер с ссылкой на обновление
            // System.Diagnostics.Process.Start(updateUrl);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in ShowUpdateDetails: {ex.Message}");
        }
    }

    private void ChangeKey(Keys newKey)
    {
        try
        {
            switch (keySelection)
            {
                case 0: KeyEngine = newKey; break;
                case 1: KeyRefuel = newKey; break;
                case 2: KeySeatbelt = newKey; break;
                case 3: KeyABS = newKey; break;
                case 4: KeyHazard = newKey; break;
                case 5: KeyLeftIndicator = newKey; break;
                case 6: KeyRightIndicator = newKey; break;
                case 7: KeyMenu = newKey; break;
                case 8: KeyCancelRefuel = newKey; break;
            }

            UpdateKeyMenuText();
            ShowNotification($"Клавиша изменена на {newKey}");
            waitingForKey = false;
        }
        catch (Exception ex)
        {
            LogToFile($"Error in ChangeKey: {ex.Message}");
            waitingForKey = false;
        }
    }

    private void CycleFuelColor()
    {
        if (fuelColor == Color.FromArgb(0, 255, 255))
            fuelColor = Color.Lime;
        else if (fuelColor == Color.Lime)
            fuelColor = Color.Red;
        else if (fuelColor == Color.Red)
            fuelColor = Color.Blue;
        else if (fuelColor == Color.Blue)
            fuelColor = Color.Orange;
        else
            fuelColor = Color.FromArgb(0, 255, 255);

        string colorName = fuelColor == Color.Lime ? "Зеленый" :
                          fuelColor == Color.Red ? "Красный" :
                          fuelColor == Color.Blue ? "Синий" :
                          fuelColor == Color.Orange ? "Оранжевый" : "Бирюзовый";
        ShowNotification($"Цвет полоски изменен на {colorName}");
    }

    // ================= УПРАВЛЕНИЕ =================
    private void OnKeyDown(object sender, KeyEventArgs e)
    {
        try
        {
            // Если ожидаем ввод новой клавиши
            if (waitingForKey)
            {
                if (e.KeyCode == Keys.Escape)
                {
                    waitingForKey = false;
                    ShowNotification("Изменение клавиши отменено");
                    return;
                }

                // Не позволяем использовать служебные клавиши
                if (e.KeyCode == Keys.F10 || e.KeyCode == Keys.Enter ||
                    e.KeyCode == Keys.Escape || e.KeyCode == Keys.Up ||
                    e.KeyCode == Keys.Down || e.KeyCode == Keys.Left ||
                    e.KeyCode == Keys.Right || e.KeyCode == Keys.Tab ||
                    e.KeyCode == Keys.F11)
                {
                    ShowNotification("Эта клавиша зарезервирована!");
                    return;
                }

                ChangeKey(e.KeyCode);
                return;
            }

            // Проверка обновлений по F11
            if (e.KeyCode == Keys.F11)
            {
                CheckForUpdates();
                return;
            }

            // МЕНЮ (F10 или настроенная клавиша)
            if (e.KeyCode == KeyMenu)
            {
                if (editingFuelBar || editingText || editingMarker)
                {
                    // Выход из режима редактирования
                    editingFuelBar = false;
                    editingText = false;
                    editingMarker = false;
                    SaveSettings();
                    ShowNotification("Настройки сохранены!");
                }
                else if (showMenu)
                {
                    // Выход из меню или возврат из подменю
                    if (editingKeys)
                    {
                        editingKeys = false;
                    }
                    else if (editingMarker)
                    {
                        editingMarker = false;
                    }
                    else
                    {
                        showMenu = false;
                    }
                }
                else
                {
                    // Открытие меню
                    showMenu = true;
                    menuSelection = 0;
                    editingKeys = false;
                    editingMarker = false;
                }
                return;
            }

            // Управление меню
            if (showMenu)
            {
                HandleMenuInput();
                return;
            }

            // Если в режиме редактирования - блокируем другие клавиши
            if (editingFuelBar || editingText || editingMarker) return;

            // ЗАПРАВКА (E или настроенная клавиша)
            if (e.KeyCode == KeyRefuel)
            {
                // Если возле заправки - начинаем заправку
                if (isNearGasStation)
                {
                    TryRefuel();
                }
                else
                {
                    ShowNotification("Подъезжайте к заправке для заправки");
                }
                return;
            }

            // ДВИГАТЕЛЬ (Y или настроенная клавиша)
            if (e.KeyCode == KeyEngine && vehicle != null)
            {
                if (vehicle.Speed > 10f && vehicle.IsEngineRunning)
                {
                    ShowNotification("Нельзя заглушить двигатель на скорости!");
                    return;
                }

                engineOn = !engineOn;
                vehicle.IsEngineRunning = engineOn;
                ShowNotification(engineOn ? "Двигатель ВКЛ" : "Двигатель ВЫКЛ");
            }

            // РЕМНИ (G или настроенная клавиша)
            else if (e.KeyCode == KeySeatbelt)
            {
                seatbelt = !seatbelt;
                ShowNotification($"Ремни {(seatbelt ? "пристегнуты" : "отстегнуты")}");
            }

            // ABS (H или настроенная клавиша)
            else if (e.KeyCode == KeyABS)
            {
                abs = !abs;
                ShowNotification($"ABS {(abs ? "ВКЛ" : "ВЫКЛ")}");
            }

            // АВАРИЙКА (Up или настроенная клавиша)
            else if (e.KeyCode == KeyHazard && vehicle != null)
            {
                hazard = !hazard;
                leftIndicator = false;
                rightIndicator = false;
            }

            // ЛЕВЫЙ ПОВОРОТНИК (Right или настроенная клавиша)
            else if (e.KeyCode == KeyLeftIndicator && vehicle != null)
            {
                leftIndicator = !leftIndicator;
                rightIndicator = false;
                hazard = false;
            }

            // ПРАВЫЙ ПОВОРОТНИК (Left или настроенная клавиша)
            else if (e.KeyCode == KeyRightIndicator && vehicle != null)
            {
                rightIndicator = !rightIndicator;
                leftIndicator = false;
                hazard = false;
            }
        }
        catch (Exception ex)
        {
            LogToFile($"Error in OnKeyDown: {ex.Message}\n{ex.StackTrace}");
        }
    }

    private void OnKeyUp(object sender, KeyEventArgs e)
    {
        // Не используется, но оставлено для совместимости
    }

    // ================= НАСТРОЙКИ =================
    private void LoadSettings()
    {
        try
        {
            var path = "scripts\\AdvancedVehicleControl.ini";
            if (!File.Exists(path))
            {
                LogToFile("Config file not found, using defaults");
                return;
            }

            var lines = File.ReadAllLines(path);
            foreach (var line in lines)
            {
                var parts = line.Split('=');
                if (parts.Length != 2) continue;

                var key = parts[0].Trim();
                var value = parts[1].Trim();

                switch (key)
                {
                    case "FuelBarX": fuelBarX = float.Parse(value); break;
                    case "FuelBarY": fuelBarY = float.Parse(value); break;
                    case "FuelBarWidth": fuelBarWidth = float.Parse(value); break;
                    case "FuelBarHeight": fuelBarHeight = float.Parse(value); break;
                    case "TextFuelX": textFuelX = float.Parse(value); break;
                    case "TextFuelY": textFuelY = float.Parse(value); break;
                    case "TextEngineX": textEngineX = float.Parse(value); break;
                    case "TextEngineY": textEngineY = float.Parse(value); break;
                    case "TextABSX": textABSX = float.Parse(value); break;
                    case "TextABSY": textABSY = float.Parse(value); break;
                    case "TextBeltX": textBeltX = float.Parse(value); break;
                    case "TextBeltY": textBeltY = float.Parse(value); break;
                    case "TextScale": textScale = float.Parse(value); break;
                    case "FuelColorR": fuelColor = Color.FromArgb(fuelColor.A, int.Parse(value), fuelColor.G, fuelColor.B); break;
                    case "FuelColorG": fuelColor = Color.FromArgb(fuelColor.A, fuelColor.R, int.Parse(value), fuelColor.B); break;
                    case "FuelColorB": fuelColor = Color.FromArgb(fuelColor.A, fuelColor.R, fuelColor.G, int.Parse(value)); break;
                    case "MarkerSize": markerSize = float.Parse(value); break;
                    case "MarkerColorR": markerColor = Color.FromArgb(markerColor.A, int.Parse(value), markerColor.G, markerColor.B); break;
                    case "MarkerColorG": markerColor = Color.FromArgb(markerColor.A, markerColor.R, int.Parse(value), markerColor.B); break;
                    case "MarkerColorB": markerColor = Color.FromArgb(markerColor.A, markerColor.R, markerColor.G, int.Parse(value)); break;
                    case "KeyEngine": KeyEngine = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyRefuel": KeyRefuel = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeySeatbelt": KeySeatbelt = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyABS": KeyABS = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyHazard": KeyHazard = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyLeftIndicator": KeyLeftIndicator = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyRightIndicator": KeyRightIndicator = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyMenu": KeyMenu = (Keys)Enum.Parse(typeof(Keys), value); break;
                    case "KeyCancelRefuel": KeyCancelRefuel = (Keys)Enum.Parse(typeof(Keys), value); break;
                }
            }
            LogToFile("Settings loaded successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"Error loading settings: {ex.Message}");
        }
    }

    private void SaveSettings()
    {
        try
        {
            var lines = new string[]
            {
                $"FuelBarX={fuelBarX}",
                $"FuelBarY={fuelBarY}",
                $"FuelBarWidth={fuelBarWidth}",
                $"FuelBarHeight={fuelBarHeight}",
                $"TextFuelX={textFuelX}",
                $"TextFuelY={textFuelY}",
                $"TextEngineX={textEngineX}",
                $"TextEngineY={textEngineY}",
                $"TextABSX={textABSX}",
                $"TextABSY={textABSY}",
                $"TextBeltX={textBeltX}",
                $"TextBeltY={textBeltY}",
                $"TextScale={textScale}",
                $"FuelColorR={fuelColor.R}",
                $"FuelColorG={fuelColor.G}",
                $"FuelColorB={fuelColor.B}",
                $"MarkerSize={markerSize}",
                $"MarkerColorR={markerColor.R}",
                $"MarkerColorG={markerColor.G}",
                $"MarkerColorB={markerColor.B}",
                $"KeyEngine={KeyEngine}",
                $"KeyRefuel={KeyRefuel}",
                $"KeySeatbelt={KeySeatbelt}",
                $"KeyABS={KeyABS}",
                $"KeyHazard={KeyHazard}",
                $"KeyLeftIndicator={KeyLeftIndicator}",
                $"KeyRightIndicator={KeyRightIndicator}",
                $"KeyMenu={KeyMenu}",
                $"KeyCancelRefuel={KeyCancelRefuel}"
            };

            File.WriteAllLines("scripts\\AdvancedVehicleControl.ini", lines);
            LogToFile("Settings saved successfully");
        }
        catch (Exception ex)
        {
            LogToFile($"Error saving settings: {ex.Message}");
        }
    }

    private void ResetSettings()
    {
        try
        {
            fuelBarX = 0.08f;
            fuelBarY = 0.88f;
            fuelBarWidth = 0.15f;
            fuelBarHeight = 0.012f;

            textFuelX = 0.005f;
            textFuelY = 0.865f;
            textEngineX = 0.055f;
            textEngineY = 0.865f;
            textABSX = 0.095f;
            textABSY = 0.865f;
            textBeltX = 0.135f;
            textBeltY = 0.865f;

            textScale = 0.30f;
            fuelColor = Color.FromArgb(0, 255, 255);

            markerSize = 2.0f;
            markerColor = Color.FromArgb(150, 0, 255, 0);

            KeyEngine = Keys.Y;
            KeyRefuel = Keys.E;
            KeySeatbelt = Keys.G;
            KeyABS = Keys.H;
            KeyHazard = Keys.Up;
            KeyLeftIndicator = Keys.Right;
            KeyRightIndicator = Keys.Left;
            KeyMenu = Keys.F10;
            KeyCancelRefuel = Keys.F;

            SaveSettings();
            LogToFile("Settings reset to defaults");
        }
        catch (Exception ex)
        {
            LogToFile($"Error resetting settings: {ex.Message}");
        }
    }

    // ================= ВСПОМОГАТЕЛЬНЫЕ МЕТОДЫ =================
    private void ShowNotification(string msg)
    {
        try
        {
            GTA.UI.Notification.Show(msg);
        }
        catch (Exception ex)
        {
            try
            {
                GTA.UI.Screen.ShowSubtitle(msg, 2000);
            }
            catch { }
        }
    }

    private void DrawText(string text, float x, float y, Color color)
    {
        try
        {
            Function.Call((Hash)0x66E0276CC5F6B9DA, 4);
            Function.Call((Hash)0x07C837F9A01C34C9, textScale, textScale);
            Function.Call((Hash)0xBE6B23FFA53FB442, color.R, color.G, color.B, 255);
            Function.Call((Hash)0x1CA3E9EAC9D93E5E);
            Function.Call((Hash)0x25FBB336DF1804CB, "STRING");
            Function.Call((Hash)0x6C188BE134E074AA, text);
            Function.Call((Hash)0xCD015E5BB0D96A57, x, y);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawText: {ex.Message}");
        }
    }

    private void DrawScreenText(string text, float x, float y, float scale, Color color)
    {
        try
        {
            Function.Call((Hash)0x66E0276CC5F6B9DA, 4);
            Function.Call((Hash)0x07C837F9A01C34C9, scale, scale);
            Function.Call((Hash)0xBE6B23FFA53FB442, color.R, color.G, color.B, 255);
            Function.Call((Hash)0x1CA3E9EAC9D93E5E);
            Function.Call((Hash)0x25FBB336DF1804CB, "STRING");
            Function.Call((Hash)0x6C188BE134E074AA, text);
            Function.Call((Hash)0xCD015E5BB0D96A57, x, y);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in DrawScreenText: {ex.Message}");
        }
    }

    private float GetTextWidth(string text)
    {
        try
        {
            Function.Call((Hash)0x07C837F9A01C34C9, textScale, textScale);
            Function.Call((Hash)0x54CE8AC98E120CAB, "STRING");
            Function.Call((Hash)0x6C188BE134E074AA, text);
            return Function.Call<float>((Hash)0x85F061DA64ED2F67, true);
        }
        catch (Exception ex)
        {
            LogToFile($"Error in GetTextWidth: {ex.Message}");
            return 0.1f;
        }
    }
}