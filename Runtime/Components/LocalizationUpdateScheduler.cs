using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace LocalizedDomain.Unity
{
public sealed class LocalizationUpdateScheduler : MonoBehaviour
{
	private static LocalizationUpdateScheduler _instance;
	private static readonly HashSet<LocalizedTextBase> Components = new();
	private static readonly Queue<QueuedItem> Queue = new();
	private static readonly HashSet<LocalizedTextBase> Queued = new();
	private static int _version;
	private static bool _processing;
	private static LocalizationService _service;

	public static int MaxPerFrame { get; set; } = 50;

	public static void Register(LocalizedTextBase component)
	{
		if (component == null)
		{
			return;
		}

		if (!Application.isPlaying)
		{
			return;
		}

		EnsureInstance();
		Components.Add(component);
		EnsureSubscription();
	}

	public static void Unregister(LocalizedTextBase component)
	{
		if (component == null)
		{
			return;
		}

		if (!Application.isPlaying)
		{
			return;
		}

		Components.Remove(component);
		Queued.Remove(component);
	}

	public static void RequestRefresh(LocalizedTextBase component)
	{
		if (component == null)
		{
			return;
		}

		if (!Application.isPlaying)
		{
			component.RefreshImmediate();
			return;
		}

		EnsureInstance();
		Enqueue(component, _version);
		StartProcessing();
	}

	private static void RefreshAll()
	{
		_version++;
		Queue.Clear();
		Queued.Clear();

		foreach (var component in Components)
		{
			if (component == null || !component.AutoRefresh)
			{
				continue;
			}

			Enqueue(component, _version);
		}

		StartProcessing();
	}

	private static void Enqueue(LocalizedTextBase component, int version)
	{
		if (component == null)
		{
			return;
		}

		if (!Queued.Add(component))
		{
			return;
		}

		Queue.Enqueue(new QueuedItem(component, version));
	}

	private static void StartProcessing()
	{
		if (_processing || _instance == null)
		{
			return;
		}

		_instance.StartCoroutine(_instance.ProcessQueue());
	}

	private IEnumerator ProcessQueue()
	{
		_processing = true;
		var batchSize = MaxPerFrame <= 0 ? int.MaxValue : MaxPerFrame;

		while (Queue.Count > 0)
		{
			var processed = 0;
			while (Queue.Count > 0 && processed < batchSize)
			{
				var item = Queue.Dequeue();
				Queued.Remove(item.Target);

				if (item.Version != _version)
				{
					continue;
				}

				if (item.Target == null || !item.Target.isActiveAndEnabled)
				{
					continue;
				}

				item.Target.RefreshImmediate();
				processed++;
			}

			if (Queue.Count > 0)
			{
				yield return null;
			}
		}

		_processing = false;
	}

	private static void EnsureSubscription()
	{
		var service = LocalizationRuntime.Service;
		if (service == null || _service == service)
		{
			return;
		}

		if (_service != null)
		{
			_service.LocaleChanged -= OnLocaleChanged;
			_service.DataReloaded -= OnDataReloaded;
		}

		_service = service;
		_service.LocaleChanged += OnLocaleChanged;
		_service.DataReloaded += OnDataReloaded;
	}

	private static void OnLocaleChanged(string _)
	{
		RefreshAll();
	}

	private static void OnDataReloaded()
	{
		RefreshAll();
	}

	private static void EnsureInstance()
	{
		if (_instance != null)
		{
			return;
		}

		var go = new GameObject("LocalizedDomain.UpdateScheduler");
		go.hideFlags = HideFlags.HideAndDontSave;
		DontDestroyOnLoad(go);
		_instance = go.AddComponent<LocalizationUpdateScheduler>();
	}

	private readonly struct QueuedItem
	{
		public QueuedItem(LocalizedTextBase target, int version)
		{
			Target = target;
			Version = version;
		}

		public LocalizedTextBase Target { get; }
		public int Version { get; }
	}
}
}