import { ComponentFixture, TestBed } from '@angular/core/testing';
import { provideRouter, ActivatedRoute } from '@angular/router';
import { of } from 'rxjs';
import { ComingSoonComponent } from './coming-soon.component';

describe('Customer ComingSoonComponent', () => {
  let fixture: ComponentFixture<ComingSoonComponent>;

  beforeEach(async () => {
    await TestBed.configureTestingModule({
      imports: [ComingSoonComponent],
      providers: [
        provideRouter([]),
        {
          provide: ActivatedRoute,
          useValue: {
            data: of({ title: 'My Bids' })
          }
        }
      ],
    }).compileComponents();

    fixture = TestBed.createComponent(ComingSoonComponent);
    fixture.detectChanges();
  });

  it('renders module title from route data', () => {
    expect(fixture.nativeElement.textContent).toContain('My Bids');
    expect(fixture.nativeElement.textContent).toContain('Coming Soon');
    expect(fixture.nativeElement.textContent).toContain('This customer module will be available in a future FarmKart phase.');
  });

  it('provides a navigation button back to /customer dashboard', () => {
    const link = fixture.nativeElement.querySelector('a[routerLink="/customer"]');
    expect(link).toBeTruthy();
    expect(link.textContent).toContain('Back to Dashboard');
  });
});
