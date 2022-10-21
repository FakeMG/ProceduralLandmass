using System;
using System.Collections.Generic;
using System.Threading;
using UnityEngine;

namespace ProceduralLandmassGeneration {
    public class ThreadedDataRequester : MonoBehaviour {
        private static ThreadedDataRequester _instance;
        private Queue<ThreadInfo> _dataQueue = new Queue<ThreadInfo>();

        private void Awake() {
            _instance = GetComponent<ThreadedDataRequester>();
        }

        public static void RequestData(Func<object> generateData, Action<object> callback) {
            void ThreadStart() {
                _instance.DataThread(generateData, callback);
            }

            new Thread(ThreadStart).Start();
        }


        private void DataThread(Func<object> generateData, Action<object> callback) {
            object data = generateData();
            lock (_dataQueue) {
                _dataQueue.Enqueue(new ThreadInfo(callback, data));
            }
        }

        private void Update() {
            if (_dataQueue.Count > 0) {
                for (int i = 0; i < _dataQueue.Count; i++) {
                    ThreadInfo threadInfo = _dataQueue.Dequeue();
                    threadInfo.Callback(threadInfo.Parameter);
                }
            }
        }
    }


    internal struct ThreadInfo {
        public readonly Action<object> Callback;
        public readonly object Parameter;

        public ThreadInfo(Action<object> callback, object parameter) {
            Callback = callback;
            Parameter = parameter;
        }
    }
}