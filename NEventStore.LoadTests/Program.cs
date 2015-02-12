using System;
using System.Diagnostics;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NEventStore.LoadTests
{
    class Program
    {
        static void Main(string[] args)
        {
            MongoHelper.DropAll();
            load_parallel();
//            load_nes();

            Console.ReadKey();
        }

        private static void load_nes()
        {
            using (var runner = new EventStreamTests())
            {
                runner.Setup();

                Parallel.For(0, 1000000, i =>
                {
                    runner.create_a_stream_with_a_new_itemCreatedEvent();
                });
            }
        }
        private static void load_parallel()
        {
            using (var runner = new EventStreamTests())
            {
                Console.WriteLine("Starting parallel tests");
                Console.WriteLine("  writers   : {0}", EventStreamTests.ParallelWriters);
                Console.WriteLine("  iterations: {0}", EventStreamTests.IterationsPerWriter);

                var sw = new Stopwatch();
                sw.Start();
                runner.load_parallel();
                sw.Stop();

                Console.WriteLine("Elapsed {0}", sw.ElapsedMilliseconds);
            }
        }
    }
}
