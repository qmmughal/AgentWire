'use client';
import { useEffect, useState } from 'react';

type AIPacket = {
  id: string;
  traceId: string;
  agentId: string;
  modelProvider: string;
  modelName: string;
  systemPrompt: string;
  userPrompt: string;
  llmResponse: string;
  promptTokens: number;
  completionTokens: number;
  cost: number;
  latencyMs: number;
  createdAt: string;
};

export default function Home() {
  const [packets, setPackets] = useState<AIPacket[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  const fetchPackets = async () => {
    try {
      const res = await fetch('http://localhost:5260/v1/packets'); // Default ASP.NET core port, could be 5000/5001
      if (!res.ok) throw new Error('Failed to fetch data');
      const data = await res.json();
      setPackets(data);
      setError(null);
    } catch (err) {
      console.error(err);
      setError('Could not connect to AgentWire API. Ensure the backend is running.');
    } finally {
      setLoading(false);
    }
  };

  useEffect(() => {
    fetchPackets();
    const interval = setInterval(fetchPackets, 5000);
    return () => clearInterval(interval);
  }, []);

  return (
    <main className="min-h-screen bg-slate-950 text-slate-200 p-8">
      <header className="mb-8 border-b border-slate-800 pb-4 flex justify-between items-center">
        <h1 className="text-3xl font-bold text-white tracking-tight">AgentWire <span className="text-blue-500">Live Traffic</span></h1>
        <div className="flex gap-4 text-sm">
          <div className="bg-slate-900 px-4 py-2 rounded-md border border-slate-800">
            <span className="text-slate-400">Total Packets: </span>
            <span className="font-mono text-emerald-400">{packets.length}</span>
          </div>
        </div>
      </header>

      {error && (
        <div className="bg-red-900/50 border border-red-500 text-red-200 px-4 py-3 rounded mb-6">
          {error}
        </div>
      )}

      <div className="bg-slate-900 border border-slate-800 rounded-lg overflow-hidden shadow-xl">
        <div className="overflow-x-auto">
          <table className="w-full text-sm text-left">
            <thead className="text-xs text-slate-400 bg-slate-950 uppercase border-b border-slate-800">
              <tr>
                <th className="px-6 py-4">Timestamp</th>
                <th className="px-6 py-4">Model</th>
                <th className="px-6 py-4">Agent</th>
                <th className="px-6 py-4">Latency (ms)</th>
                <th className="px-6 py-4">Tokens</th>
                <th className="px-6 py-4">Cost ($)</th>
              </tr>
            </thead>
            <tbody>
              {loading && packets.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-slate-500">Loading packets...</td>
                </tr>
              ) : packets.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-6 py-8 text-center text-slate-500">No traffic detected. Send packets to /v1/traces.</td>
                </tr>
              ) : (
                packets.map((p) => (
                  <tr key={p.id} className="border-b border-slate-800 hover:bg-slate-800/50 transition-colors">
                    <td className="px-6 py-4 font-mono text-xs">{new Date(p.createdAt).toLocaleTimeString()}</td>
                    <td className="px-6 py-4 font-medium text-blue-400">{p.modelName}</td>
                    <td className="px-6 py-4 text-slate-300">{p.agentId || 'Unknown'}</td>
                    <td className="px-6 py-4 font-mono">{p.latencyMs.toFixed(0)}</td>
                    <td className="px-6 py-4 font-mono text-emerald-400">{p.promptTokens + p.completionTokens}</td>
                    <td className="px-6 py-4 font-mono text-amber-400">{p.cost.toFixed(6)}</td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>
      </div>
    </main>
  );
}
