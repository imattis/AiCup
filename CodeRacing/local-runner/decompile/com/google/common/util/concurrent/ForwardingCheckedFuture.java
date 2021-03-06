package com.google.common.util.concurrent;

import com.google.common.annotations.Beta;
import com.google.common.base.Preconditions;
import java.util.concurrent.TimeUnit;
import java.util.concurrent.TimeoutException;

@Beta
public abstract class ForwardingCheckedFuture
  extends ForwardingListenableFuture
  implements CheckedFuture
{
  public Object checkedGet()
    throws Exception
  {
    return delegate().checkedGet();
  }
  
  public Object checkedGet(long paramLong, TimeUnit paramTimeUnit)
    throws TimeoutException, Exception
  {
    return delegate().checkedGet(paramLong, paramTimeUnit);
  }
  
  protected abstract CheckedFuture delegate();
  
  @Beta
  public static abstract class SimpleForwardingCheckedFuture
    extends ForwardingCheckedFuture
  {
    private final CheckedFuture delegate;
    
    protected SimpleForwardingCheckedFuture(CheckedFuture paramCheckedFuture)
    {
      this.delegate = ((CheckedFuture)Preconditions.checkNotNull(paramCheckedFuture));
    }
    
    protected final CheckedFuture delegate()
    {
      return this.delegate;
    }
  }
}


/* Location:              D:\Projects\AiCup\CodeRacing\local-runner\local-runner.jar!\com\google\common\util\concurrent\ForwardingCheckedFuture.class
 * Java compiler version: 6 (50.0)
 * JD-Core Version:       0.7.1
 */