namespace OhtohsTask;

public static class Demo
{
    public static void Main()
    {
        Console.WriteLine("=== Demo 1: Simple Run ===");
        var task = OhtohsTask.Run(() =>
        {
            Console.WriteLine("Running task...");
            Thread.Sleep(500);
            Console.WriteLine("Task done!");
        });

        task.Wait();
        Console.WriteLine("Task completed!\n");

        Console.WriteLine("=== Demo 2: ContinueWith ===");
        OhtohsTask.Run(() =>
            {
                Console.WriteLine("Task 1: Doing work...");
                Thread.Sleep(300);
            })
            .ContinueWith(() =>
            {
                Console.WriteLine("Task 2: Continuation running after Task 1.");
            })
            .Wait();

        Console.WriteLine("\n=== Demo 3: Exception Handling ===");
        try
        {
            var errorTask = OhtohsTask.Run(() =>
            {
                Console.WriteLine("Task 3: Throwing...");
                throw new InvalidOperationException("Something went wrong!");
            });

            errorTask.Wait(); // should rethrow
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Caught: {ex.Message}");
        }
    }
}