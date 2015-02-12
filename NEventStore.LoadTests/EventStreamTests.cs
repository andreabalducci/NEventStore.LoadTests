using System;
using System.Collections.Generic;
using System.Reflection;
using System.Threading;
using NEventStore.Persistence.MongoDB;
using NEventStore.Serialization;

namespace NEventStore.LoadTests
{
    public class EventStreamTests : IDisposable
    {
        private static IStoreEvents _store;
        private const string _inventoryBucket = "inventory";
        private long _counter = 0;
        public class ItemCreatedEvent
        {
            public string Id { get; set; }
        }

        public class ItemDisabledEvent
        {
        }

        public class ItemSKUChanged
        {
            public string SKU { get; set; }
        }

        static EventStreamTests()
        {
            _store = CreateStore();
        }

        public void Setup()
        {
        }

        public void Dispose()
        {
            _store.Dispose();
        }

        private string GetNextId()
        {
            return "Item_" + Interlocked.Increment(ref _counter);
        }

        public void create_a_stream_with_a_new_itemCreatedEvent()
        {
            CreateItem(GetNextId(), "ABC00123");
        }

        public void create_and_disable_Item_1()
        {
            var id = GetNextId();
            CreateItem(id, "ABC00123");
            DisableItem(id);
        }

        private void DisableItem(string itemId)
        {
            using (var stream = _store.OpenStream(_inventoryBucket, itemId, 0, int.MaxValue))
            {
                stream.Add(new EventMessage()
                {
                    Body = new ItemDisabledEvent()
                });

                stream.CommitChanges(commitId: Guid.NewGuid());
            }
        }

        private void CreateItem(string itemId, string sku)
        {
            using (var stream = _store.CreateStream(_inventoryBucket, itemId))
            {
                stream.Add(new EventMessage()
                {
                    Headers = new Dictionary<string, object>() { { "secret", "value" } },
                    Body = new ItemCreatedEvent()
                    {
                        Id = itemId
                    }
                });

                stream.Add(new EventMessage()
                {
                    Body = new ItemSKUChanged()
                    {
                        SKU = sku
                    }
                });

                FillDiagnosticHeaders(stream.UncommittedHeaders);

                stream.CommitChanges(commitId: Guid.NewGuid());
            }
        }

        private static void FillDiagnosticHeaders(IDictionary<string, object> headers)
        {
            headers.Add("author", "test");
            headers.Add("machine", Environment.MachineName);
            headers.Add("app_name", Assembly.GetExecutingAssembly().FullName);
            headers.Add("app_ver", Assembly.GetExecutingAssembly().GetName().Version);
        }

        private static IStoreEvents CreateStore()
        {
            var store = Wireup.Init()
                .UsingMongoPersistence("es", new DocumentObjectSerializer(), new MongoPersistenceOptions()
                {
                    ServerSideOptimisticLoop = true
                })
                .InitializeStorageEngine()
                .DoNotDispatchCommits()
                .Build();
            return store;
        }


        public void load_parallel()
        {
            const int ParallelWriters = 4;
            const int IterationsPerWriter = 100;

            var stop = new ManualResetEventSlim(false);
            long counter = 0;

            for (int t = 0; t < ParallelWriters; t++)
            {
                int t1 = t;
                var runner = new Thread(() =>
                {
                    for (int c = 0; c < IterationsPerWriter; c++)
                    {
                        try
                        {
                            create_and_disable_Item_1();
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine(ex.Message);
                            throw;
                        }
                    }
                    Interlocked.Increment(ref counter);
                    if (counter == ParallelWriters)
                    {
                        stop.Set();
                    }
                });

                runner.Start();
            }

            stop.Wait();
        }
    }
}