//check one - basic LazyCache

using LazyCache;
using Ninject;

IAppCache cache = new CachingService(CachingService.DefaultCacheProvider);

var item = cache.GetOrAdd("Program.Main.Person", () => Tuple.Create("Joe Blogs", DateTime.UtcNow));

Console.WriteLine(item.Item1);

//check two - using Ninject
IKernel kernel = new StandardKernel(new LazyCacheModule());
cache = kernel.Get<IAppCache>();

item = cache.GetOrAdd("Program.Main.Person", () => Tuple.Create("Joe Blogs", DateTime.UtcNow));

Console.WriteLine(item.Item1);