import tkinter as tk
from tkinter import ttk, scrolledtext, messagebox
import json
import threading
from unity_api_client_modular import UnitySceneAPI

class UnityAPITesterGUI:
    def __init__(self, root):
        self.root = root
        self.root.title("Unity Scene API Tester")
        self.root.geometry("800x600")
        
        # Инициализация Unity API
        self.unity = UnitySceneAPI()
        
        # Создание интерфейса
        self.create_widgets()
        
    def create_widgets(self):
        # Главный фрейм
        main_frame = ttk.Frame(self.root, padding="10")
        main_frame.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # Заголовок
        title_label = ttk.Label(main_frame, text="Unity Scene API Tester", font=('Arial', 16, 'bold'))
        title_label.grid(row=0, column=0, columnspan=2, pady=(0, 20))
        
        # Фрейм для кнопок
        buttons_frame = ttk.LabelFrame(main_frame, text="Тестовые функции", padding="10")
        buttons_frame.grid(row=1, column=0, sticky=(tk.W, tk.E, tk.N, tk.S), padx=(0, 10))
        
        # Кнопки для тестов
        self.create_test_buttons(buttons_frame)
        
        # Фрейм для вывода
        output_frame = ttk.LabelFrame(main_frame, text="Результат", padding="10")
        output_frame.grid(row=1, column=1, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # Текстовое поле для вывода
        self.output_text = scrolledtext.ScrolledText(output_frame, width=50, height=30, wrap=tk.WORD)
        self.output_text.grid(row=0, column=0, sticky=(tk.W, tk.E, tk.N, tk.S))
        
        # Кнопка очистки
        clear_button = ttk.Button(output_frame, text="Очистить", command=self.clear_output)
        clear_button.grid(row=1, column=0, pady=(10, 0))
        
        # Настройка растягивания
        self.root.columnconfigure(0, weight=1)
        self.root.rowconfigure(0, weight=1)
        main_frame.columnconfigure(1, weight=1)
        main_frame.rowconfigure(1, weight=1)
        output_frame.columnconfigure(0, weight=1)
        output_frame.rowconfigure(0, weight=1)
        
    def create_test_buttons(self, parent):
        """Создает кнопки для каждого теста"""
        tests = [
            ("1. Получить иерархию", self.test_get_hierarchy),
            ("2. Получить компоненты Main Camera", self.test_get_components),
            ("3. Создать объект TestObject", self.test_create_object),
            ("4. Найти объекты Camera", self.test_find_objects),
            ("5. Добавить Rigidbody к TestObject", self.test_add_component),
            ("6. Изменить позицию TestObject", self.test_modify_component),
            ("7. Получить сцены в билде", self.test_get_build_scenes),
            ("8. Удалить Rigidbody с TestObject", self.test_remove_component),
            ("9. Удалить TestObject", self.test_delete_object),
            ("10. Фильтрованная иерархия", self.test_filtered_hierarchy),
        ]
        
        for i, (text, command) in enumerate(tests):
            button = ttk.Button(parent, text=text, command=command, width=30)
            button.grid(row=i, column=0, pady=2, sticky=tk.W)
            
        # Кнопка для запуска всех тестов
        ttk.Separator(parent, orient='horizontal').grid(row=len(tests), column=0, sticky=(tk.W, tk.E), pady=10)
        run_all_button = ttk.Button(parent, text="Запустить все тесты", command=self.run_all_tests, width=30)
        run_all_button.grid(row=len(tests)+1, column=0, pady=2, sticky=tk.W)
        
    def log_output(self, message):
        """Добавляет сообщение в текстовое поле"""
        self.output_text.insert(tk.END, message + "\n")
        self.output_text.see(tk.END)
        self.root.update_idletasks()
        
    def clear_output(self):
        """Очищает текстовое поле"""
        self.output_text.delete(1.0, tk.END)
        
    def execute_test(self, test_name, test_func):
        """Выполняет тест в отдельном потоке"""
        def run_test():
            try:
                self.log_output(f"=== {test_name} ===")
                result = test_func()
                self.log_output(json.dumps(result, indent=2, ensure_ascii=False))
                self.log_output("")
            except Exception as e:
                self.log_output(f"Ошибка: {str(e)}")
                self.log_output("")
                
        thread = threading.Thread(target=run_test)
        thread.daemon = True
        thread.start()
        
    # Тестовые функции
    def test_get_hierarchy(self):
        request = {"action": "get_hierarchy"}
        return self.unity.execute_command(request)
        
    def test_get_components(self):
        request = {
            "action": "get_components",
            "params": {"object_path": "Main Camera"}
        }
        return self.unity.execute_command(request)
        
    def test_create_object(self):
        request = {
            "action": "create_object",
            "params": {"name": "TestObject", "parent_path": ""}
        }
        return self.unity.execute_command(request)
        
    def test_find_objects(self):
        request = {
            "action": "find_objects",
            "params": {"name": "Camera"}
        }
        return self.unity.execute_command(request)
        
    def test_add_component(self):
        request = {
            "action": "add_component",
            "params": {"object_path": "TestObject", "component_type": "Rigidbody"}
        }
        return self.unity.execute_command(request)
        
    def test_modify_component(self):
        request = {
            "action": "modify_component",
            "params": {
                "object_path": "TestObject",
                "component_type": "Transform",
                "properties": {"m_LocalPosition": {"x": 1.0, "y": 2.0, "z": 3.0}}
            }
        }
        return self.unity.execute_command(request)
        
    def test_get_build_scenes(self):
        request = {"action": "get_build_scenes"}
        return self.unity.execute_command(request)
        
    def test_remove_component(self):
        request = {
            "action": "remove_component",
            "params": {"object_path": "TestObject", "component_type": "Rigidbody"}
        }
        return self.unity.execute_command(request)
        
    def test_delete_object(self):
        request = {
            "action": "delete_object",
            "params": {"object_path": "TestObject"}
        }
        return self.unity.execute_command(request)
        
    def test_filtered_hierarchy(self):
        request = {
            "action": "get_hierarchy",
            "params": {"from_path": "Enemies"}
        }
        return self.unity.execute_command(request)
        
    def run_all_tests(self):
        """Запускает все тесты последовательно"""
        def run_all():
            tests = [
                ("Получение иерархии", self.test_get_hierarchy),
                ("Получение компонентов", self.test_get_components),
                ("Создание объекта", self.test_create_object),
                ("Поиск объектов", self.test_find_objects),
                ("Добавление компонента", self.test_add_component),
                ("Модификация компонента", self.test_modify_component),
                ("Получение сцен в билде", self.test_get_build_scenes),
                ("Удаление компонента", self.test_remove_component),
                ("Удаление объекта", self.test_delete_object),
                ("Фильтрованная иерархия", self.test_filtered_hierarchy),
            ]
            
            self.log_output("=== ЗАПУСК ВСЕХ ТЕСТОВ ===")
            for name, test_func in tests:
                try:
                    self.log_output(f"\n=== {name} ===")
                    result = test_func()
                    self.log_output(json.dumps(result, indent=2, ensure_ascii=False))
                except Exception as e:
                    self.log_output(f"Ошибка в тесте '{name}': {str(e)}")
                    
            self.log_output("\n=== ВСЕ ТЕСТЫ ЗАВЕРШЕНЫ ===")
            self.log_output(f"Лог сохранен в: {self.unity.get_log_file_path()}")
            
        thread = threading.Thread(target=run_all)
        thread.daemon = True
        thread.start()

def main():
    root = tk.Tk()
    app = UnityAPITesterGUI(root)
    root.mainloop()

if __name__ == "__main__":
    main()