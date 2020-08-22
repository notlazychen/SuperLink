using System;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Text;
using System.Threading;

namespace DotKcp
{
    public class EleasticStack<T>
    {
        private ConcurrentStack<T> _stack = new ConcurrentStack<T>();
        private int _time;
        private Func<int, T> _createFunc;

        public EleasticStack(Func<int, T> createFactory)
        {
            _createFunc = createFactory;
        }

        public T Get()
        {
            if (_stack.TryPop(out var result))
            {
                return result;
            }
            if(_time >= int.MaxValue)
            {
                throw new OverflowException("资源池溢出!");
            }
            var i = Interlocked.Increment(ref _time);
            result = _createFunc(i);
            return result;
        }

        public void Return(T item)
        {
            _stack.Push(item);
        }
    }
}
