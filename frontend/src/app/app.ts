import { Component } from '@angular/core';
import { RouterOutlet } from '@angular/router';
import { AiAssistantComponent } from './shared/components/ai-assistant.component';

@Component({
  selector: 'app-root',
  standalone: true,
  imports: [RouterOutlet, AiAssistantComponent],
  templateUrl: './app.html',
  styleUrl: './app.css'
})
export class App {
}
