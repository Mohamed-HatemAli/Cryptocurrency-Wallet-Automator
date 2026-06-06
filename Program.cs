using Microsoft.Playwright;

class Program
{
    public static async Task Main()
    {
        using var playwright = await Playwright.CreateAsync();

        string projectPath = @"C:\Users\Solutions\Desktop\freelans project\MetaMaskAutomator";
        string extensionPath = Path.Combine(projectPath, "metamask-extension");

        for (int walletIndex = 1; walletIndex <= 100; walletIndex++)
        {
            Console.WriteLine($"\n--- Starting Wallet #{walletIndex} ---");

            string userDataDir = Path.Combine(Path.GetTempPath(), $"metamask_profile_{walletIndex}");

            var browserContext = await playwright.Chromium.LaunchPersistentContextAsync(userDataDir, new()
            {
                Headless = false,
                Args = new[] {
                $"--disable-extensions-except={extensionPath}",
                $"--load-extension={extensionPath}",
                "--no-sandbox"
            },
                ViewportSize = new ViewportSize { Width = 1280, Height = 800 }
            });

            var page = await browserContext.WaitForPageAsync();
            await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

            try
            {
                await page.Locator("button[data-testid='onboarding-create-wallet']").ClickAsync(new() { Force = true });
                await page.Locator("button[data-testid='onboarding-create-with-srp-button']").ClickAsync(new() { Force = true });

                var pass1 = page.Locator("input[data-testid='create-password-new-input']");
                await pass1.WaitForAsync();
                await pass1.FocusAsync();
                await pass1.TypeAsync("MHA227320mha", new() { Delay = 50 });

                var pass2 = page.Locator("input[data-testid='create-password-confirm-input']");
                await pass2.FocusAsync();
                await pass2.TypeAsync("MHA227320mha", new() { Delay = 50 });

                await page.Locator("input[data-testid='create-password-terms']").ClickAsync(new() { Force = true });
                await Task.Delay(3000);

                await page.Locator("button[data-testid='create-password-submit']").ClickAsync(new() { Force = true });
                await page.Locator("button[data-testid='recovery-phrase-reveal']").ClickAsync(new() { Force = true });

                string allWords = "";
                for (int i = 0; i < 12; i++)
                {
                    var wordLocator = page.Locator($"[data-testid='recovery-phrase-chip-{i}']");
                    string wordText = await wordLocator.InnerTextAsync();
                    string cleanWord = wordText.Split('.')[1].Trim();
                    allWords += $"{i + 1}: {cleanWord}\n";
                }

                string fileName = $"Wallet_{walletIndex}_Keys.txt";
                string filePath = Path.Combine(projectPath, fileName);
                await File.WriteAllTextAsync(filePath, allWords);
                Console.WriteLine($"Saved {fileName}");

                await page.Locator("button[data-testid='recovery-phrase-continue']").ClickAsync();

                var fileLines = await File.ReadAllLinesAsync(filePath);
                string[] myWords = new string[12];
                for (int i = 0; i < fileLines.Length; i++)
                {
                    myWords[i] = fileLines[i].Split(':')[1].Trim();
                }

                var emptySlots = page.Locator("button[data-testid^='recovery-phrase-chip-']");
                int count = await emptySlots.CountAsync();
                for (int i = 0; i < count; i++)
                {
                    var slot = emptySlots.Nth(i);
                    string testId = await slot.GetAttributeAsync("data-testid");
                    int wordIndex = int.Parse(testId.Split('-').Last());
                    string correctWord = myWords[wordIndex];

                    var wordOption = page.Locator("button[data-testid^='recovery-phrase-quiz-unanswered-']")
                                         .Filter(new() { HasText = correctWord });

                    await wordOption.ScrollIntoViewIfNeededAsync();
                    await wordOption.ClickAsync(new() { Force = true });
                    await Task.Delay(3000);
                }

                await page.Locator("button[data-testid='recovery-phrase-confirm']").ClickAsync(new() { Force = true });
                await page.Locator("button[data-testid='confirm-srp-modal-button']").ClickAsync(new() { Force = true });
                await page.Locator("button[data-testid='metametrics-i-agree']").ClickAsync(new() { Force = true });
                await page.Locator("button[data-testid='onboarding-complete-done']").ClickAsync(new() { Force = true });
                await Task.Delay(5000);

                await page.GotoAsync("chrome://extensions/");
                await page.WaitForLoadStateAsync(LoadState.NetworkIdle);

                void DialogHandler(object sender, IDialog dialog)
                {
                    Task.Run(async () =>
                    {
                        Console.WriteLine("Native popup detected. Confirming removal...");
                        await dialog.AcceptAsync();
                        Console.WriteLine("Extension removed successfully!");
                    });
                }

                page.Dialog += DialogHandler;

                var mainRemoveBtn = page.Locator("extensions-item")
                                        .Filter(new() { HasText = "MetaMask" })
                                        .Locator("#removeButton");

                if (await mainRemoveBtn.CountAsync() > 0)
                {
                    await mainRemoveBtn.ClickAsync(new() { Force = true });
                    await Task.Delay(3000);
                }

                page.Dialog -= DialogHandler;

                Console.WriteLine($"--- Wallet #{walletIndex} Completed & Extension Removed ---");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error at Wallet #{walletIndex}: {ex.Message}");
            }
            finally
            {
                await browserContext.CloseAsync();
            }
        }

        Console.WriteLine("All 100 iterations finished perfectly!");
        await Task.Delay(-1);
    }
}