//
// Copyright (c) Microsoft. All rights reserved.
// Licensed under the MIT license. See LICENSE.md file in the project root for full license information.
//

namespace Au.More
{
	/// <summary>Provides a scheduler that uses STA threads.</summary>
	sealed class StaTaskScheduler_ : TaskScheduler, IDisposable
	{
		/// <summary>
		/// Static auto-created <b>StaTaskScheduler_</b> instance with 4 threads.
		/// </summary>
		public static new StaTaskScheduler_ Default => _default.Value;
		readonly static Lazy<StaTaskScheduler_> _default = new Lazy<StaTaskScheduler_>(() => new StaTaskScheduler_(4)); //info: 3-4 is optimal for getting icons

		/// <summary>Stores the queued tasks to be executed by our pool of STA threads.</summary>
		private BlockingCollection<Task> _tasks;
		/// <summary>The STA threads used by the scheduler.</summary>
		private readonly List<Thread> _threads;

		/// <summary>Initializes a new instance of the <b>StaTaskScheduler</b> class with the specified concurrency level.</summary>
		/// <param name="numberOfThreads">The number of threads that should be created and used by this scheduler.</param>
		public StaTaskScheduler_(int numberOfThreads) {
			// Validate arguments
			if (numberOfThreads < 1) throw new ArgumentOutOfRangeException(nameof(numberOfThreads));

			// Initialize the tasks collection
			_tasks = new BlockingCollection<Task>();

			// Create the threads to be used by this scheduler
			var a = new List<Thread>(numberOfThreads);
			for (int i = 0; i < numberOfThreads; i++) {
				var thread = new Thread(() => {
					// Continually get the next task and try to execute it.
					// This will continue until the scheduler is disposed and no more tasks remain.
					foreach (var t in _tasks.GetConsumingEnumerable()) {
						TryExecuteTask(t);
					}
				}) { IsBackground = true };
				thread.SetApartmentState(ApartmentState.STA);
				a.Add(thread);
			}
			_threads = a;

			// Start all of the threads
			_threads.ForEach(t => t.Start());
		}

		/// <summary>Queues a Task to be executed by this scheduler.</summary>
		/// <param name="task">The task to be executed.</param>
		protected override void QueueTask(Task task) =>
			// Push it into the blocking collection of tasks
			_tasks.Add(task);

		/// <summary>Provides a list of the scheduled tasks for the debugger to consume.</summary>
		/// <returns>An enumerable of all tasks currently scheduled.</returns>
		protected override IEnumerable<Task> GetScheduledTasks() =>
			// Serialize the contents of the blocking collection of tasks for the debugger
			_tasks.ToArray();

		/// <summary>Determines whether a Task may be inlined.</summary>
		/// <param name="task">The task to be executed.</param>
		/// <param name="taskWasPreviouslyQueued">Whether the task was previously queued.</param>
		/// <returns><c>true</c> if the task was successfully inlined; otherwise, <c>false</c>.</returns>
		protected override bool TryExecuteTaskInline(Task task, bool taskWasPreviouslyQueued) =>
			// Try to inline if the current thread is STA
			//Thread.CurrentThread.GetApartmentState() == ApartmentState.STA &&
			//	TryExecuteTask(task);
			false; //important. Never run in thread that calls Task.Wait or Task.Result.

		/// <summary>Gets the maximum concurrency level supported by this scheduler.</summary>
		public override int MaximumConcurrencyLevel => _threads.Count;

		/// <summary>
		/// Cleans up the scheduler by indicating that no more tasks will be queued.
		/// This method blocks until all threads successfully shutdown.
		/// </summary>
		public void Dispose() {
			if (_tasks != null) {
				// Indicate that no new tasks will be coming in
				_tasks.CompleteAdding();

				// Wait for all threads to finish processing tasks
				foreach (var thread in _threads) thread.Join();

				// Cleanup
				_tasks.Dispose();
				_tasks = null;
			}
		}
	}
}
