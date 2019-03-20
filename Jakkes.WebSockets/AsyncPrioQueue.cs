using System;
using System.Collections.Generic;
using System.Text;
using System.Threading;
using System.Threading.Tasks;

namespace Jakkes.WebSockets
{
    public class AsyncPrioQueue<T>
    {

        public int Count { get { return queue.Count + prioQueue.Count; } }

        public AsyncPrioQueue() : base() { }

        private AutoResetEvent _prioEnqueued = new AutoResetEvent(false);
        private AutoResetEvent _normalEnqueued = new AutoResetEvent(false);

        private Queue<T> queue = new Queue<T>();
        private Queue<T> prioQueue = new Queue<T>();

        private readonly object _lock = new object();

        /// <summary>
        /// Adds an item to the queue.
        /// </summary>
        /// <param name="item"></param>
        public void Enqueue(T item)
        {
            queue.Enqueue(item);
            _normalEnqueued.Set();
        }

        /// <summary>
        /// Adds an item to the queue of prioritized items.
        /// </summary>
        /// <param name="item"></param>
        public void EnqueuePrioritized(T item)
        {
            prioQueue.Enqueue(item);
            _prioEnqueued.Set();
        }

        /// <summary>
        /// Returns the first object in the queue. If the prioritized queue is not empty, items from it will ge dequeued first.
        /// 
        /// If both queues are empty, the call is awaited until an object is enqueued.
        /// </summary>
        /// <returns></returns>
        public async Task<T> DequeueAsync()
        {
            return await Task.Run(() => {
                lock(_lock) {
                    while (Count == 0)
                        WaitHandle.WaitAny(new WaitHandle[] { _prioEnqueued, _normalEnqueued });
                    return Dequeue();
                }
            });
        }

        /// <summary>
        /// Returns the first object in the queue. If the prioritzied queue is not empty, items from it will ge dequeued first.
        /// </summary>
        /// <returns></returns>
        public T Dequeue()
        {
            lock (_lock) {
                if (prioQueue.Count > 0)
                    return prioQueue.Dequeue();
                else if (queue.Count > 0)
                    return queue.Dequeue();
                throw new InvalidOperationException();
            }
        }
    }
}
