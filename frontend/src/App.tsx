import { platformName } from "./platform";

export function App() {
  return (
    <main>
      <h1>{platformName}</h1>
      <p>Local application scaffold is ready.</p>
    </main>
  );
}