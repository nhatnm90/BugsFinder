const fs = require('fs');

// OpenTDB trả về data chứa các ký tự HTML (vd: &quot;), hàm này giúp parse lại thành text thường
function decodeHtml(html) {
    return html.replace(/&quot;/g, '"')
               .replace(/&#039;/g, "'")
               .replace(/&amp;/g, '&')
               .replace(/&lt;/g, '<')
               .replace(/&gt;/g, '>');
}

// Xáo trộn mảng (Fisher-Yates shuffle)
function shuffleArray(array) {
    for (let i = array.length - 1; i > 0; i--) {
        const j = Math.floor(Math.random() * (i + 1));
        [array[i], array[j]] = [array[j], array[i]];
    }
    return array;
}

// API OpenTDB cho phép lấy tối đa 50 câu/lần. Để lấy nhiều hơn, ta chạy nhiều lần.
async function fetchRealQuestions(totalTarget = 100) {
    const questions = [];
    const maxPerRequest = 50;
    const requestsNeeded = Math.ceil(totalTarget / maxPerRequest);
    let currentId = 1;

    console.log(`Bắt đầu fetch ${totalTarget} câu hỏi thực tế...`);

    for (let i = 0; i < requestsNeeded; i++) {
        try {
            // Lấy câu hỏi trắc nghiệm (multiple) tiếng Anh ngẫu nhiên
            const response = await fetch(`https://opentdb.com/api.php?amount=${maxPerRequest}&type=multiple`);
            const data = await response.json();

            if (data.response_code === 0) {
                data.results.forEach(item => {
                    // Tạo mảng chứa 1 đáp án đúng và 3 đáp án sai
                    const rawOptions = [
                        { value: decodeHtml(item.correct_answer), isAnswer: true },
                        ...item.incorrect_answers.map(ans => ({ value: decodeHtml(ans), isAnswer: false }))
                    ];

                    questions.push({
                        id: currentId++,
                        question: decodeHtml(item.question),
                        options: shuffleArray(rawOptions)
                    });
                });
            }
            
            // Dừng 2 giây giữa mỗi lần request để tránh bị API chặn (Rate Limit)
            console.log(`Đã kéo được ${questions.length} câu...`);
            await new Promise(resolve => setTimeout(resolve, 2000)); 

        } catch (error) {
            console.error("Lỗi khi fetch API:", error);
        }
    }

    // Cắt cho đủ số lượng yêu cầu (nếu dư)
    const finalData = questions.slice(0, totalTarget);
    fs.writeFileSync('real_data.json', JSON.stringify(finalData, null, 4));
    console.log(`✅ Hoàn thành! Đã lưu ${finalData.length} câu hỏi vào file real_data.json`);
}

// Gọi hàm: Bạn có thể đổi số 100 thành 500, 1000 tùy ý.
fetchRealQuestions(1000);