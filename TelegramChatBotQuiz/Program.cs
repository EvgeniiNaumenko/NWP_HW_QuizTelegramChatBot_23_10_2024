using Telegram.Bot.Exceptions;
using Telegram.Bot.Polling;
using Telegram.Bot.Types.Enums;
using Telegram.Bot.Types;
using Telegram.Bot;
using Telegram.Bot.Types.ReplyMarkups;
using System.Threading;

namespace HW_08;

internal class Program
{

    // 7856765872:AAG4S8cyBPaqqhu5MzB7GxHJDqfkNnh-UIQ

    public static List<Quiz> quizList = new List<Quiz>();
    private static Dictionary<long, int> userScores = new Dictionary<long, int>();

    private static int currentQuestionIndex = 0;
    private static int currentQuiz = 0;
    private static long? lastChatId = null;
    private static bool isTakeTest = false;

    static async Task Main()
    {
        var botClient = new TelegramBotClient("7856765872:AAG4S8cyBPaqqhu5MzB7GxHJDqfkNnh-UIQ");
        using var cts = new CancellationTokenSource();


        //Начало приема не блокирует поток вызова. Прием осуществляется в пуле потоков.
        var receiverOptions = new ReceiverOptions
        {
            AllowedUpdates = Array.Empty<UpdateType>() // получаем все типы обновлений
        };
        botClient.StartReceiving(
            updateHandler: HandleUpdateAsync,
            pollingErrorHandler: HandlePollingErrorAsync,
            receiverOptions: receiverOptions,
            cancellationToken: cts.Token
        );
        var me = await botClient.GetMeAsync();

        fillQuiz(quizList); 
        Console.WriteLine($"Бот под именем @{me.Username}, запущен.");
        Console.ReadLine();
        cts.Cancel();     
       
    }

    static async Task HandleUpdateAsync(ITelegramBotClient botClient, Update update, CancellationToken cancellationToken)
    {
        if (update.Type == Telegram.Bot.Types.Enums.UpdateType.CallbackQuery)
        {
            await HandleCallbackQuery(botClient, update.CallbackQuery);
            return;
        }

        if (update.Message is not { } message)
            return;


        var chatId = message.Chat.Id;
        currentQuestionIndex = 0;
        if (lastChatId != chatId)
        {
            currentQuestionIndex = 0;
            lastChatId = chatId;
        }
        if (!isTakeTest)
        {
            await SendQuiz(botClient, chatId, cancellationToken);
        }

    }

    static async Task SendQuiz(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        var question = "Выберите категорию:";

        InlineKeyboardMarkup inlineKeyboard = new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(text: quizList[0].Name, callbackData: "0"),

            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(text: quizList[1].Name, callbackData: "1"),

            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(text: quizList[2].Name, callbackData: "2"),

            }
        });

        await botClient.SendTextMessageAsync(
            chatId: chatId,
            text: question,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );
    }

    static async Task SendQuestion(ITelegramBotClient botClient, long chatId, CancellationToken cancellationToken)
    {
        quizList[currentQuiz].countQuestion += 1;
        if (quizList[currentQuiz].checkEndQuestions())
        {
            await botClient.SendTextMessageAsync(chatId, "Все вопросы пройдены.", cancellationToken: cancellationToken);
            return;
        }

        var question = quizList[currentQuiz].Questions[currentQuestionIndex];

        InlineKeyboardMarkup inlineKeyboard = new(new[]
        {
            new[]
            {
                InlineKeyboardButton.WithCallbackData(text: question.Options[0], callbackData: "0"),
                InlineKeyboardButton.WithCallbackData(text: question.Options[1], callbackData: "1"),
            },
            new[]
            {
                InlineKeyboardButton.WithCallbackData(text: question.Options[2], callbackData: "2"),
                InlineKeyboardButton.WithCallbackData(text: question.Options[3], callbackData: "3"),
            }
        });

        //await botClient.SendTextMessageAsync(
        //    chatId: chatId,
        //    text: question.Text,
        //    replyMarkup: inlineKeyboard,
        //    cancellationToken: cancellationToken
        //);

        // photo
        await botClient.SendPhotoAsync(
            chatId: chatId,
            photo: InputFile.FromUri(quizList[currentQuiz].Questions[currentQuestionIndex].imgUrl),
            caption:question.Text,
            replyMarkup: inlineKeyboard,
            cancellationToken: cancellationToken
        );

    }


    static async Task HandleCallbackQuery(ITelegramBotClient botClient, CallbackQuery callbackQuery)
    {
        var chatId = callbackQuery.Message.Chat.Id;
        if (!isTakeTest)
        {
            var selectedQuizIndex = int.Parse(callbackQuery.Data);
            quizList[selectedQuizIndex].countQuestion = 1;
            quizList[selectedQuizIndex].correctAnswers = 0;
            currentQuiz = selectedQuizIndex;
            isTakeTest = true;
            await SendQuestion(botClient, chatId, CancellationToken.None);
            return;
        }
        else if (isTakeTest)
        {
            var selectedAnswerIndex = int.Parse(callbackQuery.Data);
            var question = quizList[currentQuiz].Questions[currentQuestionIndex];

            bool isCorrect = question.IsCorrect(selectedAnswerIndex);
            if (isCorrect)
            {
                quizList[currentQuiz].correctAnswers++;
            };
            await botClient.SendTextMessageAsync(chatId, $"{(isCorrect ? "Правильно!" : "Неправильно!")}");

            currentQuestionIndex++;
            if (!(currentQuestionIndex == quizList[currentQuiz].Questions.Count))
            {
                await SendQuestion(botClient, chatId, CancellationToken.None);
            }
            else
            {
                isTakeTest = false;
                currentQuestionIndex = 0;
                await botClient.SendTextMessageAsync(chatId, $"Ваш результат: {quizList[currentQuiz].getMyResult()} \nСпасибо за участие!");
                await SendQuiz(botClient, chatId, CancellationToken.None);
            }
        }
    }


    static Task HandlePollingErrorAsync(ITelegramBotClient botClient, Exception exception, CancellationToken cancellationToken)
    {
        var ErrorMessage = exception switch
        {
            ApiRequestException apiRequestException
                => $"Telegram API Error:\n[{apiRequestException.ErrorCode}]\n{apiRequestException.Message}",
            _ => exception.ToString()
        };

        Console.WriteLine(ErrorMessage);
        return Task.CompletedTask;
    }

    public static List<Quiz> fillQuiz(List<Quiz> quizList)
    {
        var sciFiQuiz = new Quiz("Фантастика");
        sciFiQuiz.AddQuestion(new Question(
            "Как называется планета, на которой происходят события фильма 'Дюна'?",
            new string[] { "Каладан", "Татуин", "Арракис", "Эндор" },
            "Арракис")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/1.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Кто режиссёр фильма 'Бегущий по лезвию'?",
            new string[] { "Джеймс Кэмерон", "Ридли Скотт", "Стивен Спилберг", "Джордж Лукас" },
            "Ридли Скотт")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/2.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Как зовут робота из фильма 'Звёздные войны'?",
            new string[] { "R2-D2", "C-3PO", "BB-8", "Т-800" },
            "R2-D2")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/3.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Какое вещество называют 'Спайс' в фильме 'Дюна'?",
            new string[] { "Соль", "Специи", "Руда", "Песок" },
            "Специи")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/4.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Какой герой известен фразой 'Я твой отец'?",
            new string[] { "Люк Скайуокер", "Оби-Ван Кеноби", "Дарт Вейдер", "Йода" },
            "Дарт Вейдер")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/5.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Как зовут главного героя фильма 'Матрица'?",
            new string[] { "Нео", "Морфеус", "Киборг", "Нерон" },
            "Нео")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/6.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Какой предмет является ключом для перемещений во времени в фильме 'Назад в будущее'?",
            new string[] { "Компас", "Часы", "Капсула", "Автомобиль" },
            "Автомобиль")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/7.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Кто является главным антагонистом в фильме 'Терминатор'?",
            new string[] { "Робокоп", "Т-1000", "Т-800", "Т-2000" },
            "Т-1000")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/8.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Как зовут персонажа, который охраняет портал в фильме 'Доктор Стрэндж'?",
            new string[] { "Вонг", "Мордо", "Доктор Стрэндж", "Анциент" },
            "Вонг")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/9.jpg?raw=true" });

        sciFiQuiz.AddQuestion(new Question(
            "Как называется планета, где живет Супермен?",
            new string[] { "Криптон", "Татуин", "Земля", "Марс" },
            "Криптон")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/sciFiQuiz/10.jpg?raw=true" });

        var cartoonQuiz = new Quiz("Мультфильмы");

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут главного героя мультфильма 'Король Лев'?",
            new string[] { "Муфаса", "Симба", "Скар", "Зазу" },
            "Симба")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/1.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "В каком мультфильме присутствует персонаж по имени Джинни?",
            new string[] { "Алладин", "Мулан", "Русалочка", "Покахонтас" },
            "Алладин")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/2.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут любимого питомца Рапунцель?",
            new string[] { "Макс", "Паскаль", "Свен", "Флип" },
            "Паскаль")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/3.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как называется волшебное место в мультфильме 'Моана'?",
            new string[] { "Остров Моту", "Малоке", "Тефити", "Алоха" },
            "Тефити")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/4.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут подругу Микки Мауса?",
            new string[] { "Дейзи", "Мини", "Гуфи", "Клара" },
            "Мини")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/5.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Какое животное выступает главным героем в мультфильме 'Мадагаскар'?",
            new string[] { "Лев", "Жираф", "Бегемот", "Зебра" },
            "Лев")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/6.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут медвежонка на картинке?",
            new string[] { "Тигра", "Пятачок", "Винни", "Осел" },
            "Винни")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/7.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут маленькую рыбку на картинке?",
            new string[] { "Дори", "Гилли", "Немо", "Флиппер" },
            "Немо")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/8.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Кто является главным антагонистом в мультфильме '101 далматинец'?",
            new string[] { "Круэлла", "Урсула", "Мадам Мим", "Мадам Минерв" },
            "Круэлла")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/9.jpg?raw=true" });

        cartoonQuiz.AddQuestion(new Question(
            "Как зовут зелёного огра ?",
            new string[] { "Фиона", "Шрек", "Огги", "Брюс" },
            "Шрек")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/cartoonQuiz/10.jpg?raw=true" });



        var historyQuiz = new Quiz("Историческое кино");

        historyQuiz.AddQuestion(new Question(
            "Как зовут главного героя фильма 'Гладиатор'?",
            new string[] { "Максимус", "Спартак", "Цезарь", "Октавиан" },
            "Максимус")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/1.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Какое событие показано в фильме 'Храброе сердце'?",
            new string[] { "Война Алой и Белой розы", "Шотландская война за независимость", "Война за испанское наследство", "Французская революция" },
            "Шотландская война за независимость")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/2.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Как зовут великого полководца из фильма 'Александр'?",
            new string[] { "Цезарь", "Македонский", "Ганнибал", "Октавиан" },
            "Македонский")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/3.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Какой правитель показан в фильме 'Последний самурай'?",
            new string[] { "Токугав", "Сёгун", "Император Мэйдзи", "Ода Нобунага" },
            "Император Мэйдзи")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/4.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Кто был главным героем в фильме 'Король Артур'?",
            new string[] { "Артур", "Ричард", "Генрих", "Ланселот" },
            "Артур")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/5.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Какое событие описано в фильме 'Троя'?",
            new string[] { "Троянская война", "Война Алой и Белой розы", "Французская революция", "Римская империя" },
            "Троянская война")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/6.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Как зовут римского полководца из фильма 'Цезарь'?",
            new string[] { "Август", "Цезарь", "Нерон", "Максимус" },
            "Цезарь")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/7.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Как называется город, спасённый от разрушения в фильме 'Падение Трои'?",
            new string[] { "Троя", "Афины", "Спарта", "Рим" },
            "Троя")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/8.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Как звали великого вождя монгольской империи, которого показали в фильме 'Чингисхан'?",
            new string[] { "Бату", "Темучин", "Кублай", "Чингисхан" },
            "Чингисхан")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/9.jpg?raw=true" });

        historyQuiz.AddQuestion(new Question(
            "Как звали правителя Шотландии в фильме 'Храброе сердце'?",
            new string[] { "Уильям", "Максимус", "Роберт", "Брюс" },
            "Уильям")
        { imgUrl = "https://github.com/EvgeniiNaumenko/QuizChatBotImages/blob/main/images/historyQuiz/10.jpg?raw=true" });

        quizList.Add(sciFiQuiz);
        quizList.Add(cartoonQuiz);
        quizList.Add(historyQuiz);
        return quizList;
    }
}

public class Question
{
    public string Text { get; set; }
    public string imgUrl {  get; set; }
    public string[] Options { get; set; }
    public string CorrectAnswer { get; set; }

    public Question(string text, string[] options, string correctAnswer)
    {
        Text = text;
        Options = options;
        CorrectAnswer = correctAnswer;
    }

    public bool IsCorrect(int userAnswer)
    {
        return CorrectAnswer == Options[userAnswer];
    }
}

public class Quiz
{
    public string Name { get; set; }
    public List<Question> Questions { get; set; }
    public int countQuestion = 1;
    public int correctAnswers = 0;
    private string imagePath;

    public Quiz(string name)
    {
        Name = name;
        Questions = new List<Question>();
    }
    public void AddQuestion(Question question)
    {
        Questions.Add(question);
    }
    public bool checkEndQuestions()
    {
        return Questions.Capacity == countQuestion;
    }
    public void addCorrectAnswer()
    {
        correctAnswers += 1;
    }
    public string getMyResult()
    {
        return $"{correctAnswers} / {Questions.Count}";
    }
}