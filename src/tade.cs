using System;

namespace BasicExample
{
    class Program
    {
        static void Main(string[] args)
        {
            // Print a greeting message to the console
            Console.WriteLine("Hello, Tadele! Welcome to C# programming.");

            // Ask user for their name
            Console.Write("Please enter your name: ");
            string name = Console.ReadLine();

            // Display a personalized message
            Console.WriteLine($"Nice to meet you, {name}!");

            // Simple arithmetic example
            int a = 10;
            int b = 5;
            int sum = a + b;

            Console.WriteLine($"The sum of {a} and {b} is {sum}.");

            // Wait for user to press a key before closing
            Console.WriteLine("Press any key to exit...");
            Console.ReadKey();
        }
    }
}
